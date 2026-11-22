using System.Data;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record VacationRequestListItem(Guid Id, DateOnly StartDate, DateOnly EndDate, int RequestedDays,
    string Status, string PendingLevel, bool CanCancel, DateTimeOffset CreatedAtUtc);

public sealed record VacationDecisionHistoryItem(int Sequence, string Level, string Status, string DecidedBy,
    string Comment, DateTimeOffset? DecidedAtUtc);

public sealed record VacationDecisionItem(Guid Id, string EmployeeNumber, string EmployeeName, string Company,
    string Department, DateOnly StartDate, DateOnly EndDate, int RequestedDays, string Status, string PendingLevel,
    bool CanAct, bool PersonalDataVisible, DateTimeOffset CreatedAtUtc, IReadOnlyList<VacationDecisionHistoryItem> History);

public sealed class VacationRequestService(
    TenantDbContextFactory tenantFactory,
    VacationWorkflowService workflowService,
    IConfiguration configuration)
{
    private string? PublicBaseUrl => configuration["Application:PublicBaseUrl"];

    public async Task<VacationIntervalCalculation> PreviewSelfAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        return await WorkCalendarService.CalculateVacationIntervalAsync(db, link.EmploymentId, startDate, endDate, cancellationToken);
    }

    public async Task<Guid> CreateSelfAsync(Guid requestId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty) throw new InvalidOperationException("La solicitud necesita un identificador válido.");
        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_request, permission_request IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        var existing = await db.VacationRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.EmploymentId != link.EmploymentId || existing.StartDate != startDate || existing.EndDate != endDate)
                throw new InvalidOperationException("El identificador ya pertenece a otra solicitud.");
            await transaction.CommitAsync(cancellationToken);
            return existing.Id;
        }
        if (await db.VacationRequests.AnyAsync(x => x.EmploymentId == link.EmploymentId &&
            (x.Status == VacationRequestStatuses.Pending || x.Status == VacationRequestStatuses.Approved) &&
            x.StartDate <= endDate && x.EndDate >= startDate, cancellationToken))
            throw new InvalidOperationException("El intervalo se superpone con otra solicitud pendiente o aprobada.");
        if (await db.PermissionRequests.AnyAsync(x => x.EmploymentId == link.EmploymentId &&
            (x.Status == PermissionRequestStatuses.Pending || x.Status == PermissionRequestStatuses.Approved) &&
            x.StartDate <= endDate && x.EndDate >= startDate, cancellationToken))
            throw new InvalidOperationException("El intervalo se superpone con un permiso pendiente o aprobado.");

        var calculation = await WorkCalendarService.CalculateVacationIntervalAsync(db, link.EmploymentId, startDate, endDate, cancellationToken);
        var approvalSnapshot = await workflowService.BuildSnapshotAsync(db, link.TenantId, link.EmploymentId, link.UserId, cancellationToken);
        var reservationKey = $"vacation-request:{requestId:N}";
        var request = new VacationRequest
        {
            Id = requestId,
            EmploymentId = link.EmploymentId,
            StartDate = startDate,
            EndDate = endDate,
            RequestedDays = calculation.DaysToReserve,
            ReservationKey = reservationKey,
            RequestedByUserId = link.UserId,
            ApprovalSteps = approvalSnapshot.Select(step => new VacationApprovalStep
            {
                Sequence = step.Sequence,
                Name = step.Name,
                Type = step.Type,
                Status = step.Status,
                DepartmentId = step.DepartmentId,
                CompanyId = step.CompanyId,
                Actors = step.ActorUserIds.Select(userId => new VacationApprovalStepActor { UserId = userId }).ToList()
            }).ToList(),
            Dates = calculation.Dates.Select(x => new VacationRequestDay
            {
                Date = x.Date,
                WorkScheduleId = x.WorkScheduleId,
                WorkScheduleName = x.WorkScheduleName,
                IsScheduledWorkday = x.IsScheduledWorkday,
                IsHoliday = x.IsHoliday,
                HolidayName = x.HolidayName,
                CountsAsVacationDay = x.CountsAsVacationDay
            }).ToList()
        };
        db.VacationRequests.Add(request);
        var reservations = await VacationService.ReserveInTransactionAsync(db, link.EmploymentId, calculation.DaysToReserve, reservationKey, cancellationToken);
        db.VacationRequestAllocations.AddRange(reservations.Select(x => new VacationRequestAllocation
        {
            VacationRequestId = request.Id,
            VacationPeriodId = x.VacationPeriodId,
            ReservationMovementId = x.Id,
            LegalDays = x.LegalDays,
            AdditionalDays = x.AdditionalDays
        }));
        var employeeName = await EmployeeNameAsync(db, link.EmploymentId, cancellationToken);
        NotificationOutboxService.EnqueueRequestEvent(
            db,
            $"vacation:{request.Id:N}:submitted",
            "SUBMITTED",
            request.Id,
            request.ApprovalSteps[0].Actors.Select(x => x.UserId).ToList(),
            Content(request, employeeName, "Pendiente", request.ApprovalSteps[0].Name, string.Empty, "/tenant/solicitudes"),
            PublicBaseUrl);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task<List<VacationRequestListItem>> GetSelfRequestsAsync(CancellationToken cancellationToken = default)
    {
        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        var cancellationAllowed = (await db.VacationWorkflowSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken))?.AllowEmployeeCancellation ?? true;
        var requests = await db.VacationRequests.AsNoTracking().Include(x => x.ApprovalSteps)
            .Where(x => x.EmploymentId == link.EmploymentId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return requests.Select(x => new VacationRequestListItem(x.Id, x.StartDate, x.EndDate, x.RequestedDays, x.Status,
            x.ApprovalSteps.Where(s => s.Status == VacationApprovalStepStatuses.Pending).OrderBy(s => s.Sequence).Select(s => s.Name).FirstOrDefault() ??
                (x.Status == VacationRequestStatuses.Pending ? "Regularización administrativa" : string.Empty),
            cancellationAllowed && x.Status is VacationRequestStatuses.Pending or VacationRequestStatuses.Approved, x.CreatedAtUtc)).ToList();
    }

    public async Task<List<VacationDecisionItem>> GetWorkflowRequestsAsync(CancellationToken cancellationToken = default)
    {
        var actor = await workflowService.GetWorkflowActorAsync(false, cancellationToken);
        await using var db = tenantFactory.Create(actor.SchemaName);
        var query = db.VacationRequests.AsNoTracking().Include(x => x.ApprovalSteps).ThenInclude(x => x.Actors).AsQueryable();
        var operational = actor.IsApprover || actor.IsHumanResources;
        if (operational && !actor.IsAdministrator)
            query = query.Where(x => x.ApprovalSteps.Any(step => step.Actors.Any(a => a.UserId == actor.UserId)));
        else if (!actor.IsAdministrator) throw new UnauthorizedAccessException("No tiene acceso al circuito de vacaciones.");
        var requests = await query.OrderByDescending(x => x.Status == VacationRequestStatuses.Pending).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var visibleRequestIds = requests.Where(x => operational && x.ApprovalSteps.Any(step => step.Actors.Any(a => a.UserId == actor.UserId)))
            .Select(x => x.Id).ToList();
        var personalData = await db.VacationRequests.AsNoTracking().Where(x => visibleRequestIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Employment.EmployeeNumber, x.Employment.Person.GivenNames, x.Employment.Person.FamilyNames, Company = x.Employment.Company.LegalName })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var departmentIds = requests.SelectMany(x => x.ApprovalSteps).Select(x => x.DepartmentId).Distinct().ToList();
        var departments = await db.Departments.AsNoTracking().Where(x => departmentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var userIds = requests.SelectMany(x => x.ApprovalSteps).Where(x => x.DecidedByUserId != null).Select(x => x.DecidedByUserId!).Distinct().ToList();
        var userNames = await workflowService.GetUserNamesAsync(actor.TenantId, userIds, cancellationToken);
        return requests.Select(request =>
        {
            var current = request.ApprovalSteps.SingleOrDefault(x => x.Status == VacationApprovalStepStatuses.Pending);
            var scopeStep = current ?? request.ApprovalSteps.OrderBy(x => x.Sequence).FirstOrDefault();
            var personalDataVisible = personalData.TryGetValue(request.Id, out var person);
            var canAct = operational && request.Status == VacationRequestStatuses.Pending && current is not null &&
                current.Actors.Any(x => x.UserId == actor.UserId) && request.RequestedByUserId != actor.UserId &&
                ((current.Type == VacationApprovalLevelTypes.Approver && actor.IsApprover) ||
                 (current.Type == VacationApprovalLevelTypes.HumanResources && actor.IsHumanResources));
            return new VacationDecisionItem(request.Id, personalDataVisible ? person!.EmployeeNumber : "—",
                personalDataVisible ? person!.GivenNames + " " + person.FamilyNames : "Datos restringidos",
                personalDataVisible ? person!.Company : "—",
                personalDataVisible && scopeStep is not null ? departments.GetValueOrDefault(scopeStep.DepartmentId, "—") : "—",
                request.StartDate, request.EndDate, request.RequestedDays, request.Status,
                current?.Name ?? (request.Status == VacationRequestStatuses.Pending ? "Regularización administrativa" : string.Empty),
                canAct, personalDataVisible, request.CreatedAtUtc,
                request.ApprovalSteps.OrderBy(x => x.Sequence).Select(step => new VacationDecisionHistoryItem(step.Sequence, step.Name, step.Status,
                    step.DecidedByUserId is null ? string.Empty : userNames.GetValueOrDefault(step.DecidedByUserId, "Usuario"),
                    step.DecisionComment, step.DecidedAtUtc)).ToList());
        }).ToList();
    }

    public Task ApproveAsync(Guid requestId, string comment, CancellationToken cancellationToken = default) => ResolveAsync(requestId, true, comment, cancellationToken);

    public Task RejectAsync(Guid requestId, string comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment)) throw new InvalidOperationException("Indique el motivo del rechazo.");
        return ResolveAsync(requestId, false, comment, cancellationToken);
    }

    public async Task CancelSelfAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var link = await workflowService.GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        if (!((await db.VacationWorkflowSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken))?.AllowEmployeeCancellation ?? true))
            throw new InvalidOperationException("La cancelación de solicitudes está deshabilitada por el administrador.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        var request = await LockRequestAsync(db, requestId, cancellationToken);
        if (request.EmploymentId != link.EmploymentId) throw new UnauthorizedAccessException("La solicitud no pertenece al funcionario autenticado.");
        if (request.Status == VacationRequestStatuses.Cancelled) { await transaction.CommitAsync(cancellationToken); return; }
        if (request.Status == VacationRequestStatuses.Pending)
            await VacationService.ResolveReservationInTransactionAsync(db, request.ReservationKey, VacationMovementTypes.Release, "release:", cancellationToken);
        else if (request.Status == VacationRequestStatuses.Approved)
            await VacationService.ReverseConsumptionInTransactionAsync(db, "consume:" + request.ReservationKey, cancellationToken);
        else throw new InvalidOperationException("Una solicitud rechazada no requiere cancelación.");
        request.Status = VacationRequestStatuses.Cancelled;
        request.CancelledAtUtc = DateTimeOffset.UtcNow;
        var steps = await db.VacationApprovalSteps.Include(x => x.Actors).Where(x => x.VacationRequestId == requestId).ToListAsync(cancellationToken);
        steps.Where(x => x.Status == VacationApprovalStepStatuses.Pending || x.Status == VacationApprovalStepStatuses.Waiting)
            .ToList().ForEach(x => x.Status = VacationApprovalStepStatuses.Cancelled);
        var employeeName = await EmployeeNameAsync(db, request.EmploymentId, cancellationToken);
        var actors = steps.SelectMany(x => x.Actors).Select(x => x.UserId).ToList();
        NotificationOutboxService.EnqueueRequestEvent(
            db,
            $"vacation:{request.Id:N}:cancelled",
            "CANCELLED",
            request.Id,
            actors,
            Content(request, employeeName, "Cancelada", string.Empty, string.Empty, "/tenant/solicitudes"),
            PublicBaseUrl);
        NotificationOutboxService.EnqueueRequestEvent(
            db,
            $"vacation:{request.Id:N}:cancelled",
            "CANCELLED",
            request.Id,
            [request.RequestedByUserId],
            Content(request, employeeName, "Cancelada", string.Empty, string.Empty, "/empleado/vacaciones"),
            PublicBaseUrl);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ResolveAsync(Guid requestId, bool approve, string comment, CancellationToken cancellationToken)
    {
        var actor = await workflowService.GetWorkflowActorAsync(true, cancellationToken);
        await using var db = tenantFactory.Create(actor.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        var request = await LockRequestAsync(db, requestId, cancellationToken);
        var steps = await db.VacationApprovalSteps.Include(x => x.Actors).Where(x => x.VacationRequestId == requestId)
            .OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        if (steps.Any(x => x.DecidedByUserId == actor.UserId &&
            ((approve && x.Status == VacationApprovalStepStatuses.Approved) || (!approve && x.Status == VacationApprovalStepStatuses.Rejected))))
        { await transaction.CommitAsync(cancellationToken); return; }
        if (request.Status != VacationRequestStatuses.Pending) throw new InvalidOperationException("La solicitud ya no está pendiente.");
        var current = steps.SingleOrDefault(x => x.Status == VacationApprovalStepStatuses.Pending)
            ?? throw new InvalidOperationException("La solicitud requiere regularización administrativa antes de continuar.");
        if (request.RequestedByUserId == actor.UserId) throw new UnauthorizedAccessException("No puede resolver su propia solicitud.");
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
            steps.Where(x => x.Status == VacationApprovalStepStatuses.Waiting).ToList().ForEach(x => x.Status = VacationApprovalStepStatuses.Cancelled);
            await VacationService.ResolveReservationInTransactionAsync(db, request.ReservationKey, VacationMovementTypes.Release, "release:", cancellationToken);
            request.Status = VacationRequestStatuses.Rejected;
            request.DecisionComment = comment.Trim(); request.DecidedByUserId = actor.UserId; request.DecidedAtUtc = DateTimeOffset.UtcNow;
            recipients = [request.RequestedByUserId];
            eventType = "REJECTED";
            eventKey = $"vacation:{request.Id:N}:rejected";
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
                eventKey = $"vacation:{request.Id:N}:step:{current.Sequence}:approved";
                state = "Pendiente";
                pendingLevel = next.Name;
            }
            else
            {
                await VacationService.ResolveReservationInTransactionAsync(db, request.ReservationKey, VacationMovementTypes.Consume, "consume:", cancellationToken);
                request.Status = VacationRequestStatuses.Approved;
                request.DecisionComment = comment.Trim(); request.DecidedByUserId = actor.UserId; request.DecidedAtUtc = DateTimeOffset.UtcNow;
                recipients = [request.RequestedByUserId];
                eventType = "APPROVED";
                eventKey = $"vacation:{request.Id:N}:approved";
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
                eventType == "NEXT_LEVEL" ? "/tenant/solicitudes" : "/empleado/vacaciones"),
            PublicBaseUrl);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<VacationRequest> LockRequestAsync(TenantDbContext db, Guid requestId, CancellationToken cancellationToken) =>
        await db.VacationRequests.FromSqlInterpolated($"SELECT * FROM vacation_request WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("La solicitud no existe.");

    private static async Task<string> EmployeeNameAsync(TenantDbContext db, Guid employmentId, CancellationToken cancellationToken) =>
        await db.Employments.AsNoTracking().Where(x => x.Id == employmentId)
            .Select(x => x.Person.GivenNames + " " + x.Person.FamilyNames)
            .SingleAsync(cancellationToken);

    private static RequestNotificationContent Content(
        VacationRequest request,
        string employeeName,
        string state,
        string pendingLevel,
        string reason,
        string relativePath) => new(
            "VACATION",
            employeeName,
            $"{request.StartDate:dd/MM/yyyy}–{request.EndDate:dd/MM/yyyy}",
            $"{request.RequestedDays} día(s)",
            state,
            pendingLevel,
            reason,
            relativePath);

}
