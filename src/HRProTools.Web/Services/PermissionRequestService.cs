using System.Data;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record PermissionRequestListItem(
    Guid Id,
    string PermissionType,
    string Unit,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int RequestedDays,
    int RequestedMinutes,
    string Reason,
    string Status,
    string PendingLevel,
    bool CanCancel,
    string CancellationReason,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<VacationDecisionHistoryItem> History);

public sealed record PermissionDecisionItem(
    Guid Id,
    string EmployeeNumber,
    string EmployeeName,
    string Company,
    string Department,
    string PermissionType,
    string Unit,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int RequestedDays,
    int RequestedMinutes,
    string Reason,
    string Status,
    string PendingLevel,
    bool CanAct,
    bool PersonalDataVisible,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<VacationDecisionHistoryItem> History);

public sealed class PermissionRequestService(
    TenantDbContextFactory tenantFactory,
    VacationWorkflowService workflowService,
    IConfiguration configuration)
{
    private string? PublicBaseUrl => configuration["Application:PublicBaseUrl"];

    public async Task<PermissionIntervalCalculation> PreviewSelfAsync(
        string unit,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        CancellationToken cancellationToken = default)
    {
        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        var calculation = await CalculateAsync(db, link.EmploymentId, unit, startDate, endDate, startTime, endTime, cancellationToken);
        if (await HasOverlapAsync(db, link.EmploymentId, calculation, null, cancellationToken))
            throw new InvalidOperationException("El intervalo se superpone con otra ausencia pendiente o aprobada.");
        return calculation;
    }

    public async Task<Guid> CreateSelfAsync(
        Guid requestId,
        string permissionType,
        string unit,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty) throw new InvalidOperationException("La solicitud necesita un identificador válido.");
        permissionType = permissionType.Trim().ToUpperInvariant();
        if (permissionType is not PermissionRequestTypes.Paid and not PermissionRequestTypes.Unpaid)
            throw new InvalidOperationException("El tipo de permiso no está admitido.");
        reason = reason.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Indique el motivo del permiso.");
        if (reason.Length > 1000) throw new InvalidOperationException("El motivo no puede superar 1000 caracteres.");

        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE permission_request, vacation_request IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        var existing = await db.PermissionRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.EmploymentId != link.EmploymentId || existing.PermissionType != permissionType || existing.Unit != unit ||
                existing.StartDate != startDate || existing.EndDate != endDate || existing.StartTime != startTime || existing.EndTime != endTime)
                throw new InvalidOperationException("El identificador ya pertenece a otra solicitud.");
            await transaction.CommitAsync(cancellationToken);
            return existing.Id;
        }

        var calculation = await CalculateAsync(db, link.EmploymentId, unit, startDate, endDate, startTime, endTime, cancellationToken);
        if (await HasOverlapAsync(db, link.EmploymentId, calculation, null, cancellationToken))
            throw new InvalidOperationException("El intervalo se superpone con otra ausencia pendiente o aprobada.");
        var snapshot = await workflowService.BuildSnapshotAsync(db, link.TenantId, link.EmploymentId, link.UserId, cancellationToken);
        var first = snapshot[0];
        var request = new PermissionRequest
        {
            Id = requestId,
            EmploymentId = link.EmploymentId,
            CompanyId = first.CompanyId,
            DepartmentId = first.DepartmentId,
            PermissionType = permissionType,
            Unit = calculation.Unit,
            StartDate = calculation.StartDate,
            EndDate = calculation.EndDate,
            StartTime = calculation.StartTime,
            EndTime = calculation.EndTime,
            RequestedDays = calculation.RequestedDays,
            RequestedMinutes = calculation.RequestedMinutes,
            Reason = reason,
            RequestedByUserId = link.UserId,
            Dates = calculation.Dates.Select(ToEntity).ToList(),
            ApprovalSteps = snapshot.Select(ToEntity).ToList()
        };
        db.PermissionRequests.Add(request);
        var employeeName = await EmployeeNameAsync(db, link.EmploymentId, cancellationToken);
        NotificationOutboxService.EnqueueRequestEvent(
            db,
            $"permission:{request.Id:N}:submitted",
            "SUBMITTED",
            request.Id,
            request.ApprovalSteps[0].Actors.Select(x => x.UserId).ToList(),
            Content(request, employeeName, "Pendiente", request.ApprovalSteps[0].Name, string.Empty, "/tenant/solicitudes"),
            PublicBaseUrl);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task<List<PermissionRequestListItem>> GetSelfRequestsAsync(CancellationToken cancellationToken = default)
    {
        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        var cancellationAllowed = (await db.VacationWorkflowSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken))?.AllowEmployeeCancellation ?? true;
        var requests = await db.PermissionRequests.AsNoTracking().Include(x => x.ApprovalSteps)
            .Where(x => x.EmploymentId == link.EmploymentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var userIds = requests.SelectMany(x => x.ApprovalSteps).Where(x => x.DecidedByUserId != null).Select(x => x.DecidedByUserId!).Distinct().ToList();
        var userNames = await workflowService.GetUserNamesAsync(link.TenantId, userIds, cancellationToken);
        return requests.Select(x => new PermissionRequestListItem(
            x.Id, x.PermissionType, x.Unit, x.StartDate, x.EndDate, x.StartTime, x.EndTime, x.RequestedDays, x.RequestedMinutes,
            x.Reason, x.Status, PendingLevel(x), cancellationAllowed && x.Status is PermissionRequestStatuses.Pending or PermissionRequestStatuses.Approved,
            x.CancellationReason, x.CreatedAtUtc, History(x.ApprovalSteps, userNames))).ToList();
    }

    public async Task<List<PermissionDecisionItem>> GetWorkflowRequestsAsync(CancellationToken cancellationToken = default)
    {
        var actor = await workflowService.GetWorkflowActorAsync(false, cancellationToken);
        await using var db = tenantFactory.Create(actor.SchemaName);
        var query = db.PermissionRequests.AsNoTracking().Include(x => x.ApprovalSteps).ThenInclude(x => x.Actors).AsQueryable();
        var operational = actor.IsApprover || actor.IsHumanResources;
        if (operational && !actor.IsAdministrator)
            query = query.Where(x => x.ApprovalSteps.Any(step => step.Actors.Any(a => a.UserId == actor.UserId)));
        else if (!actor.IsAdministrator)
            throw new UnauthorizedAccessException("No tiene acceso al circuito de permisos.");
        var requests = await query.OrderByDescending(x => x.Status == PermissionRequestStatuses.Pending)
            .ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var visibleIds = requests.Where(x => operational && x.ApprovalSteps.Any(step => step.Actors.Any(a => a.UserId == actor.UserId)))
            .Select(x => x.Id).ToList();
        var personalData = await db.PermissionRequests.AsNoTracking().Where(x => visibleIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Employment.EmployeeNumber,
                x.Employment.Person.GivenNames,
                x.Employment.Person.FamilyNames,
                Company = x.Employment.Company.LegalName
            }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var departmentIds = requests.SelectMany(x => x.ApprovalSteps).Select(x => x.DepartmentId).Distinct().ToList();
        var departments = await db.Departments.AsNoTracking().Where(x => departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var userIds = requests.SelectMany(x => x.ApprovalSteps).Where(x => x.DecidedByUserId != null)
            .Select(x => x.DecidedByUserId!).Distinct().ToList();
        var userNames = await workflowService.GetUserNamesAsync(actor.TenantId, userIds, cancellationToken);
        return requests.Select(request =>
        {
            var current = request.ApprovalSteps.SingleOrDefault(x => x.Status == VacationApprovalStepStatuses.Pending);
            var scopeStep = current ?? request.ApprovalSteps.OrderBy(x => x.Sequence).FirstOrDefault();
            var personalVisible = personalData.TryGetValue(request.Id, out var person);
            var canAct = operational && request.Status == PermissionRequestStatuses.Pending && current is not null &&
                current.Actors.Any(x => x.UserId == actor.UserId) && request.RequestedByUserId != actor.UserId &&
                ((current.Type == VacationApprovalLevelTypes.Approver && actor.IsApprover) ||
                 (current.Type == VacationApprovalLevelTypes.HumanResources && actor.IsHumanResources));
            return new PermissionDecisionItem(
                request.Id,
                personalVisible ? person!.EmployeeNumber : "—",
                personalVisible ? person!.GivenNames + " " + person.FamilyNames : "Datos restringidos",
                personalVisible ? person!.Company : "—",
                personalVisible && scopeStep is not null ? departments.GetValueOrDefault(scopeStep.DepartmentId, "—") : "—",
                request.PermissionType, request.Unit, request.StartDate, request.EndDate, request.StartTime, request.EndTime,
                request.RequestedDays, request.RequestedMinutes, personalVisible ? request.Reason : string.Empty, request.Status, PendingLevel(request), canAct,
                personalVisible, request.CreatedAtUtc, History(request.ApprovalSteps, userNames));
        }).ToList();
    }

    public Task ApproveAsync(Guid requestId, string comment, CancellationToken cancellationToken = default) =>
        ResolveAsync(requestId, true, comment, cancellationToken);

    public Task RejectAsync(Guid requestId, string comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment)) throw new InvalidOperationException("Indique el motivo del rechazo.");
        return ResolveAsync(requestId, false, comment, cancellationToken);
    }

    public async Task CancelSelfAsync(Guid requestId, string reason, CancellationToken cancellationToken = default)
    {
        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        if (!((await db.VacationWorkflowSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken))?.AllowEmployeeCancellation ?? true))
            throw new InvalidOperationException("La cancelación de solicitudes está deshabilitada por el administrador.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await LockRequestAsync(db, requestId, cancellationToken);
        if (request.EmploymentId != link.EmploymentId)
            throw new UnauthorizedAccessException("La solicitud no pertenece al funcionario autenticado.");
        if (request.Status == PermissionRequestStatuses.Cancelled)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (request.Status is not PermissionRequestStatuses.Pending and not PermissionRequestStatuses.Approved)
            throw new InvalidOperationException("Una solicitud rechazada no requiere cancelación.");
        var steps = await db.PermissionApprovalSteps.Include(x => x.Actors)
            .Where(x => x.PermissionRequestId == requestId).OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        steps.Where(x => x.Status is VacationApprovalStepStatuses.Pending or VacationApprovalStepStatuses.Waiting)
            .ToList().ForEach(x => x.Status = VacationApprovalStepStatuses.Cancelled);
        request.Status = PermissionRequestStatuses.Cancelled;
        request.CancelledByUserId = link.UserId;
        request.CancellationReason = reason.Trim();
        request.CancelledAtUtc = DateTimeOffset.UtcNow;
        var employeeName = await EmployeeNameAsync(db, request.EmploymentId, cancellationToken);
        var actors = steps.SelectMany(x => x.Actors).Select(x => x.UserId).ToList();
        NotificationOutboxService.EnqueueRequestEvent(
            db,
            $"permission:{request.Id:N}:cancelled",
            "CANCELLED",
            request.Id,
            actors,
            Content(request, employeeName, "Cancelada", string.Empty, request.CancellationReason, "/tenant/solicitudes"),
            PublicBaseUrl);
        NotificationOutboxService.EnqueueRequestEvent(
            db,
            $"permission:{request.Id:N}:cancelled",
            "CANCELLED",
            request.Id,
            [request.RequestedByUserId],
            Content(request, employeeName, "Cancelada", string.Empty, request.CancellationReason, "/empleado/permisos"),
            PublicBaseUrl);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ResolveAsync(Guid requestId, bool approve, string comment, CancellationToken cancellationToken)
    {
        var actor = await workflowService.GetWorkflowActorAsync(true, cancellationToken);
        await using var db = tenantFactory.Create(actor.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await LockRequestAsync(db, requestId, cancellationToken);
        var steps = await db.PermissionApprovalSteps.Include(x => x.Actors)
            .Where(x => x.PermissionRequestId == requestId).OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        if (steps.Any(x => x.DecidedByUserId == actor.UserId &&
            ((approve && x.Status == VacationApprovalStepStatuses.Approved) || (!approve && x.Status == VacationApprovalStepStatuses.Rejected))))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (request.Status != PermissionRequestStatuses.Pending)
            throw new InvalidOperationException("La solicitud ya no está pendiente.");
        var current = steps.SingleOrDefault(x => x.Status == VacationApprovalStepStatuses.Pending)
            ?? throw new InvalidOperationException("La solicitud no tiene un nivel pendiente.");
        if (request.RequestedByUserId == actor.UserId)
            throw new UnauthorizedAccessException("No puede resolver su propia solicitud.");
        var hasRole = current.Type == VacationApprovalLevelTypes.Approver ? actor.IsApprover : actor.IsHumanResources;
        if (!hasRole || !current.Actors.Any(x => x.UserId == actor.UserId))
            throw new UnauthorizedAccessException("La solicitud no está dentro de su nivel o alcance operativo.");

        current.DecidedByUserId = actor.UserId;
        current.DecisionComment = comment.Trim();
        current.DecidedAtUtc = DateTimeOffset.UtcNow;
        IReadOnlyCollection<string> recipients;
        string eventType;
        string eventKey;
        string state;
        string pendingLevel = string.Empty;
        if (!approve)
        {
            current.Status = VacationApprovalStepStatuses.Rejected;
            steps.Where(x => x.Status == VacationApprovalStepStatuses.Waiting).ToList()
                .ForEach(x => x.Status = VacationApprovalStepStatuses.Cancelled);
            request.Status = PermissionRequestStatuses.Rejected;
            request.DecisionComment = comment.Trim();
            request.DecidedByUserId = actor.UserId;
            request.DecidedAtUtc = DateTimeOffset.UtcNow;
            recipients = [request.RequestedByUserId];
            eventType = "REJECTED";
            eventKey = $"permission:{request.Id:N}:rejected";
            state = "Rechazada";
        }
        else
        {
            current.Status = VacationApprovalStepStatuses.Approved;
            var next = steps.FirstOrDefault(x => x.Sequence > current.Sequence && x.Status == VacationApprovalStepStatuses.Waiting);
            if (next is not null)
            {
                next.Status = VacationApprovalStepStatuses.Pending;
                recipients = next.Actors.Select(x => x.UserId).ToList();
                eventType = "NEXT_LEVEL";
                eventKey = $"permission:{request.Id:N}:step:{current.Sequence}:approved";
                state = "Pendiente";
                pendingLevel = next.Name;
            }
            else
            {
                request.Status = PermissionRequestStatuses.Approved;
                request.DecisionComment = comment.Trim();
                request.DecidedByUserId = actor.UserId;
                request.DecidedAtUtc = DateTimeOffset.UtcNow;
                recipients = [request.RequestedByUserId];
                eventType = "APPROVED";
                eventKey = $"permission:{request.Id:N}:approved";
                state = "Aprobada";
            }
        }
        var employeeName = await EmployeeNameAsync(db, request.EmploymentId, cancellationToken);
        NotificationOutboxService.EnqueueRequestEvent(
            db,
            eventKey,
            eventType,
            request.Id,
            recipients,
            Content(request, employeeName, state, pendingLevel, approve ? string.Empty : comment.Trim(),
                eventType == "NEXT_LEVEL" ? "/tenant/solicitudes" : "/empleado/permisos"),
            PublicBaseUrl);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<PermissionIntervalCalculation> CalculateAsync(
        TenantDbContext db,
        Guid employmentId,
        string unit,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        CancellationToken cancellationToken)
    {
        unit = unit.Trim().ToUpperInvariant();
        return unit switch
        {
            PermissionRequestUnits.Days => await WorkCalendarService.CalculatePermissionDaysAsync(db, employmentId, startDate, endDate, cancellationToken),
            PermissionRequestUnits.Hours when startTime is not null && endTime is not null =>
                await WorkCalendarService.CalculatePermissionHoursAsync(db, employmentId, startDate, startTime.Value, endTime.Value, cancellationToken),
            PermissionRequestUnits.Hours => throw new InvalidOperationException("Indique la hora inicial y final."),
            _ => throw new InvalidOperationException("La unidad del permiso no está admitida.")
        };
    }

    private static async Task<bool> HasOverlapAsync(
        TenantDbContext db,
        Guid employmentId,
        PermissionIntervalCalculation calculation,
        Guid? excludedRequestId,
        CancellationToken cancellationToken)
    {
        if (await db.VacationRequests.AsNoTracking().AnyAsync(x => x.EmploymentId == employmentId &&
            (x.Status == VacationRequestStatuses.Pending || x.Status == VacationRequestStatuses.Approved) &&
            x.StartDate <= calculation.EndDate && x.EndDate >= calculation.StartDate, cancellationToken))
            return true;
        var candidates = await db.PermissionRequests.AsNoTracking().Where(x => x.EmploymentId == employmentId &&
            x.Id != excludedRequestId &&
            (x.Status == PermissionRequestStatuses.Pending || x.Status == PermissionRequestStatuses.Approved) &&
            x.StartDate <= calculation.EndDate && x.EndDate >= calculation.StartDate).ToListAsync(cancellationToken);
        foreach (var existing in candidates)
        {
            if (calculation.Unit == PermissionRequestUnits.Days || existing.Unit == PermissionRequestUnits.Days)
                return true;
            if (existing.StartDate == calculation.StartDate && existing.StartTime < calculation.EndTime && existing.EndTime > calculation.StartTime)
                return true;
        }
        return false;
    }

    private static PermissionRequestDay ToEntity(PermissionDateCalculation x) => new()
    {
        Date = x.Date,
        WorkScheduleId = x.WorkScheduleId,
        WorkScheduleName = x.WorkScheduleName,
        WorkStart = x.WorkStart,
        WorkEnd = x.WorkEnd,
        BreakStart = x.BreakStart,
        BreakEnd = x.BreakEnd,
        IsScheduledWorkday = x.IsScheduledWorkday,
        IsHoliday = x.IsHoliday,
        HolidayName = x.HolidayName,
        CountsAsPermissionDay = x.CountsAsPermissionDay,
        EffectiveMinutes = x.EffectiveMinutes
    };

    private static PermissionApprovalStep ToEntity(ApprovalStepSnapshot x) => new()
    {
        Sequence = x.Sequence,
        Name = x.Name,
        Type = x.Type,
        Status = x.Status,
        DepartmentId = x.DepartmentId,
        CompanyId = x.CompanyId,
        Actors = x.ActorUserIds.Select(userId => new PermissionApprovalStepActor { UserId = userId }).ToList()
    };

    private static async Task<PermissionRequest> LockRequestAsync(TenantDbContext db, Guid requestId, CancellationToken cancellationToken) =>
        await db.PermissionRequests.FromSqlInterpolated($"SELECT * FROM permission_request WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("La solicitud no existe.");

    private static string PendingLevel(PermissionRequest request) => request.ApprovalSteps
        .Where(x => x.Status == VacationApprovalStepStatuses.Pending).OrderBy(x => x.Sequence)
        .Select(x => x.Name).FirstOrDefault() ?? string.Empty;

    private static IReadOnlyList<VacationDecisionHistoryItem> History(
        IEnumerable<PermissionApprovalStep> steps,
        IReadOnlyDictionary<string, string> userNames) => steps.OrderBy(x => x.Sequence)
        .Select(step => new VacationDecisionHistoryItem(
            step.Sequence,
            step.Name,
            step.Status,
            step.DecidedByUserId is null ? string.Empty : userNames.GetValueOrDefault(step.DecidedByUserId, "Usuario"),
            step.DecisionComment,
            step.DecidedAtUtc)).ToList();

    private static async Task<string> EmployeeNameAsync(TenantDbContext db, Guid employmentId, CancellationToken cancellationToken) =>
        await db.Employments.AsNoTracking().Where(x => x.Id == employmentId)
            .Select(x => x.Person.GivenNames + " " + x.Person.FamilyNames)
            .SingleAsync(cancellationToken);

    private static RequestNotificationContent Content(
        PermissionRequest request,
        string employeeName,
        string state,
        string pendingLevel,
        string decisionReason,
        string relativePath) => new(
            "PERMISSION",
            employeeName,
            request.Unit == PermissionRequestUnits.Hours
                ? $"{request.StartDate:dd/MM/yyyy} {request.StartTime:HH\\:mm}–{request.EndTime:HH\\:mm}"
                : $"{request.StartDate:dd/MM/yyyy}–{request.EndDate:dd/MM/yyyy}",
            request.Unit == PermissionRequestUnits.Hours
                ? $"{request.RequestedMinutes / 60} h {request.RequestedMinutes % 60:D2} min"
                : $"{request.RequestedDays} día(s)",
            state,
            pendingLevel,
            decisionReason,
            relativePath);
}
