using System.Data;
using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
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
    CurrentTenantService currentTenant,
    TenantDbContextFactory tenantFactory,
    IServiceScopeFactory scopeFactory,
    AuthenticationStateProvider authenticationStateProvider,
    VacationWorkflowService workflowService)
{
    public async Task<VacationIntervalCalculation> PreviewSelfAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var link = await GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        return await WorkCalendarService.CalculateVacationIntervalAsync(db, link.EmploymentId, startDate, endDate, cancellationToken);
    }

    public async Task<Guid> CreateSelfAsync(Guid requestId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty) throw new InvalidOperationException("La solicitud necesita un identificador válido.");
        var link = await GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
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

        var calculation = await WorkCalendarService.CalculateVacationIntervalAsync(db, link.EmploymentId, startDate, endDate, cancellationToken);
        var approvalSteps = await workflowService.BuildSnapshotAsync(db, link.TenantId, link.EmploymentId, link.UserId, cancellationToken);
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
            ApprovalSteps = approvalSteps,
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
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task<List<VacationRequestListItem>> GetSelfRequestsAsync(CancellationToken cancellationToken = default)
    {
        var link = await GetSelfLinkAsync(cancellationToken);
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
        var actor = await GetWorkflowActorAsync(false, cancellationToken);
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
        var userNames = await GetUserNamesAsync(actor.TenantId, userIds, cancellationToken);
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
        var link = await GetSelfLinkAsync(cancellationToken);
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
        var steps = await db.VacationApprovalSteps.Where(x => x.VacationRequestId == requestId &&
            (x.Status == VacationApprovalStepStatuses.Pending || x.Status == VacationApprovalStepStatuses.Waiting)).ToListAsync(cancellationToken);
        steps.ForEach(x => x.Status = VacationApprovalStepStatuses.Cancelled);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ResolveAsync(Guid requestId, bool approve, string comment, CancellationToken cancellationToken)
    {
        var actor = await GetWorkflowActorAsync(true, cancellationToken);
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
        if (!approve)
        {
            current.Status = VacationApprovalStepStatuses.Rejected;
            steps.Where(x => x.Status == VacationApprovalStepStatuses.Waiting).ToList().ForEach(x => x.Status = VacationApprovalStepStatuses.Cancelled);
            await VacationService.ResolveReservationInTransactionAsync(db, request.ReservationKey, VacationMovementTypes.Release, "release:", cancellationToken);
            request.Status = VacationRequestStatuses.Rejected;
            request.DecisionComment = comment.Trim(); request.DecidedByUserId = actor.UserId; request.DecidedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            current.Status = VacationApprovalStepStatuses.Approved;
            var next = steps.FirstOrDefault(x => x.Sequence > current.Sequence && x.Status == VacationApprovalStepStatuses.Waiting);
            if (next is not null) next.Status = VacationApprovalStepStatuses.Pending;
            else
            {
                await VacationService.ResolveReservationInTransactionAsync(db, request.ReservationKey, VacationMovementTypes.Consume, "consume:", cancellationToken);
                request.Status = VacationRequestStatuses.Approved;
                request.DecisionComment = comment.Trim(); request.DecidedByUserId = actor.UserId; request.DecidedAtUtc = DateTimeOffset.UtcNow;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<VacationRequest> LockRequestAsync(TenantDbContext db, Guid requestId, CancellationToken cancellationToken) =>
        await db.VacationRequests.FromSqlInterpolated($"SELECT * FROM vacation_request WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("La solicitud no existe.");

    private async Task<(string UserId, Guid TenantId, string SchemaName, Guid EmploymentId)> GetSelfLinkAsync(CancellationToken cancellationToken)
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (!user.IsInRole(AppRoles.Employee)) throw new UnauthorizedAccessException("No tiene acceso al autoservicio del funcionario.");
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employmentId = await globalDb.Users.AsNoTracking().Where(x => x.Id == userId && x.TenantId == tenant.Id && x.IsActive && x.ActiveEmploymentId != null)
            .Select(x => x.ActiveEmploymentId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("La cuenta no está vinculada a un funcionario activo.");
        return (userId, tenant.Id, tenant.SchemaName, employmentId);
    }

    private async Task<(string UserId, Guid TenantId, string SchemaName, bool IsAdministrator, bool IsApprover, bool IsHumanResources)> GetWorkflowActorAsync(
        bool requireOperationalRole, CancellationToken cancellationToken)
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var isAdministrator = user.IsInRole(AppRoles.TenantAdministrator);
        var isApprover = user.IsInRole(AppRoles.Approver);
        var isHumanResources = user.IsInRole(AppRoles.HumanResources);
        if ((requireOperationalRole && !(isApprover || isHumanResources)) || (!requireOperationalRole && !(isAdministrator || isApprover || isHumanResources)))
            throw new UnauthorizedAccessException("No tiene acceso operativo al circuito de vacaciones.");
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await globalDb.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.TenantId == tenant.Id && x.IsActive, cancellationToken))
            throw new UnauthorizedAccessException("La cuenta no está activa en este tenant.");
        return (userId, tenant.Id, tenant.SchemaName, isAdministrator, isApprover, isHumanResources);
    }

    private async Task<Dictionary<string, string>> GetUserNamesAsync(Guid tenantId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.AsNoTracking().Where(x => x.TenantId == tenantId && userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
    }
}
