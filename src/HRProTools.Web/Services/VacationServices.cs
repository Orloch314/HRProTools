using System.Data;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record VacationBalance(decimal Recognized, decimal Reserved, decimal Consumed, decimal Available);
public sealed record VacationPeriodView(Guid Id, DateOnly ReferenceStart, DateOnly ReferenceEnd, DateOnly AccruedOn, DateOnly UseBy, decimal LegalDays, decimal AdditionalDays, decimal LegalRemaining, decimal Reserved, decimal Consumed, decimal Available, bool IsPending, string Origin);
public sealed record VacationSummary(DateOnly StartDate, int CompletedYears, DateOnly NextAccrualDate, decimal CurrentLegalDays, decimal CurrentAdditionalDays, VacationBalance Balance, IReadOnlyList<VacationPeriodView> Periods);

public static class ParaguayVacationRules
{
    public static decimal LegalDays(int completedYears) => completedYears switch { <= 0 => 0, <= 5 => 12, <= 10 => 18, _ => 30 };
    public static int CompletedYears(DateOnly start, DateOnly onDate)
    {
        var years = onDate.Year - start.Year;
        if (start.AddYears(years) > onDate) years--;
        return Math.Max(0, years);
    }
    public static DateOnly ParaguayToday()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Asuncion");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
    }
}

public static class VacationLedgerQueries
{
    public static async Task<VacationBalance> GetEmploymentBalanceAsync(TenantDbContext db, Guid employmentId, CancellationToken ct = default)
    {
        var periods = await db.VacationPeriods.AsNoTracking().Where(x => x.EmploymentId == employmentId).Select(x => x.Id).ToListAsync(ct);
        var movements = await db.VacationMovements.AsNoTracking().Where(x => periods.Contains(x.VacationPeriodId)).ToListAsync(ct);
        return Calculate(movements);
    }

    public static VacationBalance Calculate(IEnumerable<VacationMovement> movements)
    {
        decimal recognized = 0, reserved = 0, consumed = 0;
        foreach (var m in movements)
        {
            var days = m.LegalDays + m.AdditionalDays;
            switch (m.Type)
            {
                case VacationMovementTypes.Credit: recognized += days; break;
                case VacationMovementTypes.Adjustment: recognized += days; break;
                case VacationMovementTypes.Reserve: reserved += days; break;
                case VacationMovementTypes.Release: reserved -= days; break;
                case VacationMovementTypes.Consume:
                    consumed += days;
                    if (m.SourceMovementId is not null) reserved -= days;
                    break;
                case VacationMovementTypes.Reversal: consumed -= days; break;
            }
        }
        return new(recognized, Math.Max(0, reserved), Math.Max(0, consumed), recognized - consumed - Math.Max(0, reserved));
    }
}

public sealed class VacationService(CurrentTenantService currentTenant, TenantDbContextFactory factory)
{
    public async Task<List<VacationPolicy>> GetPoliciesAsync(CancellationToken ct = default)
    {
        await using var db = await OpenAsync(ct);
        return await db.VacationPolicies.AsNoTracking().Include(x => x.Company).OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct);
    }

    public async Task SavePolicyAsync(Guid companyId, DateOnly effectiveFrom, decimal days1To5, decimal days6To10, decimal daysOver10, CancellationToken ct = default)
    {
        if (days1To5 < 0 || days6To10 < 0 || daysOver10 < 0) throw new InvalidOperationException("Los días adicionales no pueden ser negativos.");
        await using var db = await OpenAsync(ct);
        db.VacationPolicies.Add(new VacationPolicy { CompanyId = companyId, EffectiveFrom = effectiveFrom, AdditionalDaysYears1To5 = days1To5, AdditionalDaysYears6To10 = days6To10, AdditionalDaysOver10 = daysOver10 });
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GenerateDuePeriodsAsync(DateOnly? asOf = null, CancellationToken ct = default)
    {
        var today = asOf ?? ParaguayVacationRules.ParaguayToday();
        await using var db = await OpenAsync(ct);
        return await GenerateDuePeriodsAsync(db, today, ct);
    }

    public static async Task<int> GenerateDuePeriodsAsync(TenantDbContext db, DateOnly today, CancellationToken ct = default)
    {
        var employments = await db.Employments.Where(x => x.Status == EmploymentStatuses.Active && x.StartDate.AddYears(1) <= today).ToListAsync(ct);
        var created = 0;
        foreach (var employment in employments)
        {
            var latestAccrued = await db.VacationPeriods.Where(x => x.EmploymentId == employment.Id).MaxAsync(x => (DateOnly?)x.AccruedOn, ct);
            DateOnly anniversary;
            if (latestAccrued is null)
            {
                var years = ParaguayVacationRules.CompletedYears(employment.StartDate, today);
                anniversary = employment.StartDate.AddYears(years); // no historical backfill when prior use is unknown
            }
            else anniversary = latestAccrued.Value.AddYears(1);

            while (anniversary <= today)
            {
                var years = ParaguayVacationRules.CompletedYears(employment.StartDate, anniversary);
                var policy = await db.VacationPolicies.Where(x => x.CompanyId == employment.CompanyId && x.EffectiveFrom <= anniversary)
                    .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
                var additional = policy is null ? 0 : years <= 5 ? policy.AdditionalDaysYears1To5 : years <= 10 ? policy.AdditionalDaysYears6To10 : policy.AdditionalDaysOver10;
                var period = new VacationPeriod
                {
                    EmploymentId = employment.Id, ReferenceStart = anniversary.AddYears(-1), ReferenceEnd = anniversary.AddDays(-1),
                    AccruedOn = anniversary, UseBy = anniversary.AddMonths(6), LegalDays = ParaguayVacationRules.LegalDays(years),
                    AdditionalDays = additional, VacationPolicyId = policy?.Id
                };
                if (!await db.VacationPeriods.AnyAsync(x => x.EmploymentId == employment.Id && x.ReferenceStart == period.ReferenceStart && x.ReferenceEnd == period.ReferenceEnd, ct))
                {
                    db.VacationPeriods.Add(period);
                    db.VacationMovements.Add(new VacationMovement { VacationPeriod = period, Type = VacationMovementTypes.Credit, LegalDays = period.LegalDays, AdditionalDays = period.AdditionalDays, OperationKey = $"accrual:{employment.Id:N}:{period.AccruedOn:yyyyMMdd}", Note = "Causación automática" });
                    created++;
                    await db.SaveChangesAsync(ct);
                }
                anniversary = anniversary.AddYears(1);
            }
        }
        return created;
    }

    public async Task<VacationSummary?> GetSummaryAsync(Guid employmentId, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var today = asOf ?? ParaguayVacationRules.ParaguayToday();
        await using var db = await OpenAsync(ct);
        var employment = await db.Employments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == employmentId, ct);
        if (employment is null) return null;
        var periods = await db.VacationPeriods.AsNoTracking().Where(x => x.EmploymentId == employmentId).OrderByDescending(x => x.AccruedOn).ToListAsync(ct);
        var ids = periods.Select(x => x.Id).ToList();
        var movements = await db.VacationMovements.AsNoTracking().Where(x => ids.Contains(x.VacationPeriodId)).ToListAsync(ct);
        var views = periods.Select(period =>
        {
            var own = movements.Where(x => x.VacationPeriodId == period.Id).ToList();
            var balance = VacationLedgerQueries.Calculate(own);
            var legalNet = period.LegalDays - own.Where(x => x.Type == VacationMovementTypes.Consume).Sum(x => x.LegalDays) + own.Where(x => x.Type == VacationMovementTypes.Reversal).Sum(x => x.LegalDays);
            return new VacationPeriodView(period.Id, period.ReferenceStart, period.ReferenceEnd, period.AccruedOn, period.UseBy, period.LegalDays, period.AdditionalDays, legalNet, balance.Reserved, balance.Consumed, balance.Available, today > period.UseBy && legalNet > 0, period.Origin);
        }).ToList();
        var years = ParaguayVacationRules.CompletedYears(employment.StartDate, today);
        var next = employment.StartDate.AddYears(years + 1);
        var currentPolicy = await db.VacationPolicies.Where(x => x.CompanyId == employment.CompanyId && x.EffectiveFrom <= next).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        var nextYears = years + 1;
        var additional = currentPolicy is null ? 0 : nextYears <= 5 ? currentPolicy.AdditionalDaysYears1To5 : nextYears <= 10 ? currentPolicy.AdditionalDaysYears6To10 : currentPolicy.AdditionalDaysOver10;
        return new(employment.StartDate, years, next, ParaguayVacationRules.LegalDays(nextYears), additional, VacationLedgerQueries.Calculate(movements), views);
    }

    public async Task<IReadOnlyList<VacationMovement>> ReserveAsync(Guid employmentId, decimal days, string operationKey, CancellationToken ct = default)
    {
        if (days <= 0 || string.IsNullOrWhiteSpace(operationKey)) throw new InvalidOperationException("La reserva requiere días y una clave de operación válidos.");
        await using var db = await OpenAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", ct);
        var existing = await db.VacationMovements.Where(x => x.OperationKey == operationKey && x.Type == VacationMovementTypes.Reserve).ToListAsync(ct);
        if (existing.Count > 0) return existing;
        var periods = await db.VacationPeriods.Where(x => x.EmploymentId == employmentId).OrderBy(x => x.AccruedOn).ToListAsync(ct);
        var movements = await db.VacationMovements.Where(x => periods.Select(p => p.Id).Contains(x.VacationPeriodId)).ToListAsync(ct);
        var remaining = days;
        var result = new List<VacationMovement>();
        foreach (var period in periods)
        {
            var own = movements.Where(x => x.VacationPeriodId == period.Id).ToList();
            var legalAvailable = ComponentAvailable(own, true);
            var legal = Math.Min(remaining, legalAvailable);
            remaining -= legal;
            var additional = remaining > 0 ? Math.Min(remaining, ComponentAvailable(own, false)) : 0;
            remaining -= additional;
            if (legal + additional > 0)
            {
                var item = new VacationMovement { VacationPeriodId = period.Id, Type = VacationMovementTypes.Reserve, LegalDays = legal, AdditionalDays = additional, OperationKey = operationKey, Note = "Reserva FIFO" };
                db.VacationMovements.Add(item); result.Add(item);
            }
            if (remaining <= 0) break;
        }
        if (remaining > 0) throw new InvalidOperationException("El saldo disponible es insuficiente.");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }

    public Task<IReadOnlyList<VacationMovement>> ConsumeReservationAsync(string reservationKey, CancellationToken ct = default) => ResolveReservationAsync(reservationKey, VacationMovementTypes.Consume, "consume:", ct);
    public Task<IReadOnlyList<VacationMovement>> ReleaseReservationAsync(string reservationKey, CancellationToken ct = default) => ResolveReservationAsync(reservationKey, VacationMovementTypes.Release, "release:", ct);

    public async Task<IReadOnlyList<VacationMovement>> ReverseConsumptionAsync(string consumptionKey, CancellationToken ct = default)
    {
        await using var db = await OpenAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", ct);
        var key = "reversal:" + consumptionKey;
        var existing = await db.VacationMovements.Where(x => x.OperationKey == key).ToListAsync(ct);
        if (existing.Count > 0) return existing;
        var consumed = await db.VacationMovements.Where(x => x.OperationKey == consumptionKey && x.Type == VacationMovementTypes.Consume).ToListAsync(ct);
        if (consumed.Count == 0) throw new InvalidOperationException("No existe el consumo indicado.");
        var result = consumed.Select(x => new VacationMovement { VacationPeriodId = x.VacationPeriodId, Type = VacationMovementTypes.Reversal, LegalDays = x.LegalDays, AdditionalDays = x.AdditionalDays, OperationKey = key, SourceMovementId = x.Id, Note = "Reversión de consumo" }).ToList();
        db.VacationMovements.AddRange(result); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }

    private async Task<IReadOnlyList<VacationMovement>> ResolveReservationAsync(string reservationKey, string type, string prefix, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE vacation_movement IN SHARE ROW EXCLUSIVE MODE", ct);
        var key = prefix + reservationKey;
        var existing = await db.VacationMovements.Where(x => x.OperationKey == key && x.Type == type).ToListAsync(ct);
        if (existing.Count > 0) return existing;
        var reserved = await db.VacationMovements.Where(x => x.OperationKey == reservationKey && x.Type == VacationMovementTypes.Reserve).ToListAsync(ct);
        if (reserved.Count == 0) throw new InvalidOperationException("No existe la reserva indicada.");
        var result = reserved.Select(x => new VacationMovement { VacationPeriodId = x.VacationPeriodId, Type = type, LegalDays = x.LegalDays, AdditionalDays = x.AdditionalDays, OperationKey = key, SourceMovementId = x.Id, Note = type == VacationMovementTypes.Consume ? "Consumo definitivo" : "Liberación de reserva" }).ToList();
        db.VacationMovements.AddRange(result); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }

    private static decimal ComponentAvailable(IEnumerable<VacationMovement> values, bool legal)
    {
        decimal amount = 0, reserved = 0;
        foreach (var x in values)
        {
            var days = legal ? x.LegalDays : x.AdditionalDays;
            if (x.Type is VacationMovementTypes.Credit or VacationMovementTypes.Adjustment or VacationMovementTypes.Reversal) amount += days;
            else if (x.Type == VacationMovementTypes.Consume) { amount -= days; if (x.SourceMovementId is not null) reserved -= days; }
            else if (x.Type == VacationMovementTypes.Reserve) reserved += days;
            else if (x.Type == VacationMovementTypes.Release) reserved -= days;
        }
        return Math.Max(0, amount - Math.Max(0, reserved));
    }

    private async Task<TenantDbContext> OpenAsync(CancellationToken ct) => factory.Create((await currentTenant.GetRequiredAsync(ct)).SchemaName);
}

public sealed class VacationAccrualHostedService(IServiceScopeFactory scopes, TenantDbContextFactory tenantFactory, ILogger<VacationAccrualHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var global = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var schemas = await global.Tenants.AsNoTracking().Where(x => x.Status == TenantStatuses.Active).Select(x => x.SchemaName).ToListAsync(stoppingToken);
                foreach (var schema in schemas)
                {
                    await using var db = tenantFactory.Create(schema);
                    await VacationService.GenerateDuePeriodsAsync(db, ParaguayVacationRules.ParaguayToday(), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "No fue posible ejecutar la causación automática de vacaciones."); }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
