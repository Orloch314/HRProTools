using System.Data;
using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record VacationRequestListItem(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record VacationDecisionItem(
    Guid Id,
    string EmployeeNumber,
    string EmployeeName,
    string Company,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    DateTimeOffset CreatedAtUtc);

public sealed class VacationRequestService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory tenantFactory,
    IServiceScopeFactory scopeFactory,
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorizationService)
{
    public async Task<VacationIntervalCalculation> PreviewSelfAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var link = await GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        return await WorkCalendarService.CalculateVacationIntervalAsync(
            db,
            link.EmploymentId,
            startDate,
            endDate,
            cancellationToken);
    }

    public async Task<Guid> CreateSelfAsync(
        Guid requestId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
            throw new InvalidOperationException("La solicitud necesita un identificador válido.");
        var link = await GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", cancellationToken);

        var existing = await db.VacationRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.EmploymentId != link.EmploymentId || existing.StartDate != startDate || existing.EndDate != endDate)
                throw new InvalidOperationException("El identificador ya pertenece a otra solicitud.");
            await transaction.CommitAsync(cancellationToken);
            return existing.Id;
        }

        var overlaps = await db.VacationRequests.AnyAsync(x => x.EmploymentId == link.EmploymentId &&
            (x.Status == VacationRequestStatuses.Pending || x.Status == VacationRequestStatuses.Approved) &&
            x.StartDate <= endDate && x.EndDate >= startDate, cancellationToken);
        if (overlaps)
            throw new InvalidOperationException("El intervalo se superpone con otra solicitud pendiente o aprobada.");

        var calculation = await WorkCalendarService.CalculateVacationIntervalAsync(
            db,
            link.EmploymentId,
            startDate,
            endDate,
            cancellationToken);
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
        var reservations = await VacationService.ReserveInTransactionAsync(
            db,
            link.EmploymentId,
            calculation.DaysToReserve,
            reservationKey,
            cancellationToken);
        var allocations = reservations.Select(x => new VacationRequestAllocation
        {
            VacationRequestId = request.Id,
            VacationPeriodId = x.VacationPeriodId,
            ReservationMovementId = x.Id,
            LegalDays = x.LegalDays,
            AdditionalDays = x.AdditionalDays
        }).ToList();
        db.VacationRequestAllocations.AddRange(allocations);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task<List<VacationRequestListItem>> GetSelfRequestsAsync(CancellationToken cancellationToken = default)
    {
        var link = await GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        return await db.VacationRequests.AsNoTracking()
            .Where(x => x.EmploymentId == link.EmploymentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new VacationRequestListItem(x.Id, x.StartDate, x.EndDate, x.RequestedDays, x.Status, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<VacationDecisionItem>> GetPendingForDecisionAsync(CancellationToken cancellationToken = default)
    {
        var actor = await GetDecisionActorAsync(cancellationToken);
        await using var db = tenantFactory.Create(actor.SchemaName);
        return await db.VacationRequests.AsNoTracking()
            .Where(x => x.Status == VacationRequestStatuses.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new VacationDecisionItem(
                x.Id,
                x.Employment.EmployeeNumber,
                x.Employment.Person.GivenNames + " " + x.Employment.Person.FamilyNames,
                x.Employment.Company.LegalName,
                x.StartDate,
                x.EndDate,
                x.RequestedDays,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid requestId, string comment, CancellationToken cancellationToken = default)
    {
        var actor = await GetDecisionActorAsync(cancellationToken);
        await ResolveAsync(requestId, actor.SchemaName, actor.UserId, VacationRequestStatuses.Approved, comment, cancellationToken);
    }

    public async Task RejectAsync(Guid requestId, string comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new InvalidOperationException("Indique el motivo del rechazo.");
        var actor = await GetDecisionActorAsync(cancellationToken);
        await ResolveAsync(requestId, actor.SchemaName, actor.UserId, VacationRequestStatuses.Rejected, comment, cancellationToken);
    }

    public async Task CancelSelfAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var link = await GetSelfLinkAsync(cancellationToken);
        await using var db = tenantFactory.Create(link.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        var request = await LockRequestAsync(db, requestId, cancellationToken);
        if (request.EmploymentId != link.EmploymentId)
            throw new UnauthorizedAccessException("La solicitud no pertenece al funcionario autenticado.");
        if (request.Status == VacationRequestStatuses.Cancelled)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (request.Status == VacationRequestStatuses.Pending)
        {
            await VacationService.ResolveReservationInTransactionAsync(
                db, request.ReservationKey, VacationMovementTypes.Release, "release:", cancellationToken);
        }
        else if (request.Status == VacationRequestStatuses.Approved)
        {
            await VacationService.ReverseConsumptionInTransactionAsync(
                db, "consume:" + request.ReservationKey, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Una solicitud rechazada no requiere cancelación.");
        }
        request.Status = VacationRequestStatuses.Cancelled;
        request.CancelledAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ResolveAsync(
        Guid requestId,
        string schemaName,
        string actorUserId,
        string targetStatus,
        string comment,
        CancellationToken cancellationToken)
    {
        await using var db = tenantFactory.Create(schemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        var request = await LockRequestAsync(db, requestId, cancellationToken);
        if (request.Status == targetStatus)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (request.Status != VacationRequestStatuses.Pending)
            throw new InvalidOperationException("La solicitud ya no está pendiente.");

        if (targetStatus == VacationRequestStatuses.Approved)
        {
            await VacationService.ResolveReservationInTransactionAsync(
                db, request.ReservationKey, VacationMovementTypes.Consume, "consume:", cancellationToken);
        }
        else
        {
            await VacationService.ResolveReservationInTransactionAsync(
                db, request.ReservationKey, VacationMovementTypes.Release, "release:", cancellationToken);
        }
        request.Status = targetStatus;
        request.DecisionComment = comment.Trim();
        request.DecidedByUserId = actorUserId;
        request.DecidedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<VacationRequest> LockRequestAsync(
        TenantDbContext db,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return await db.VacationRequests.FromSqlInterpolated(
                $"SELECT * FROM vacation_request WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("La solicitud no existe.");
    }

    private async Task<(string UserId, string SchemaName, Guid EmploymentId)> GetSelfLinkAsync(CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (!(await authorizationService.AuthorizeAsync(state.User, AppPolicies.EmployeeSelfService)).Succeeded)
            throw new UnauthorizedAccessException("No tiene acceso al autoservicio del funcionario.");
        var userId = state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employmentId = await globalDb.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.TenantId == tenant.Id && x.IsActive && x.ActiveEmploymentId != null)
            .Select(x => x.ActiveEmploymentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("La cuenta no está vinculada a un funcionario activo.");
        return (userId, tenant.SchemaName, employmentId);
    }

    private async Task<(string UserId, string SchemaName)> GetDecisionActorAsync(CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (!(await authorizationService.AuthorizeAsync(state.User, AppPolicies.EmployeeRecords)).Succeeded)
            throw new UnauthorizedAccessException("No tiene permiso para resolver solicitudes.");
        var userId = state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        return (userId, tenant.SchemaName);
    }
}
