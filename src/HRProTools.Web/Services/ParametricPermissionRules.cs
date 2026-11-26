using HRProTools.Web.Data;

namespace HRProTools.Web.Services;

public static class ParametricPermissionRules
{
    public static async Task<PermissionIntervalCalculation> CalculateAsync(TenantDbContext db, Guid employment, LegalPermissionType rule, string unit, DateOnly start, DateOnly end, TimeOnly? from, TimeOnly? to, CancellationToken ct)
    {
        PermissionIntervalCalculation result;
        if (rule.Unit == "WEEKS")
        {
            if (unit != "DAYS" || (end.DayNumber - start.DayNumber + 1) % 7 != 0) throw new InvalidOperationException("La licencia se registra en semanas consecutivas completas.");
            var dates = await WorkCalendarService.ResolvePermissionCalendarAsync(db, employment, start, end, ct);
            result = new(employment, "DAYS", start, end, null, null, end.DayNumber - start.DayNumber + 1, 0, dates.Select(d => d with { CountsAsPermissionDay = true }).ToList());
        }
        else if (unit == "DAYS" && rule.Unit is "DAYS" or "DAYS_OR_HOURS") result = await WorkCalendarService.CalculatePermissionDaysAsync(db, employment, start, end, ct);
        else if (unit == "HOURS" && rule.Unit is "HOURS" or "MINUTES" or "DAYS_OR_HOURS" && start == end && from.HasValue && to.HasValue) result = await WorkCalendarService.CalculatePermissionHoursAsync(db, employment, start, from.Value, to.Value, ct);
        else throw new InvalidOperationException("La unidad o intervalo no coincide con la versión publicada del permiso.");
        if (rule.MaximumQuantity.HasValue && Quantity(rule, result) > rule.MaximumQuantity) throw new InvalidOperationException($"Duración máxima publicada: {rule.MaximumQuantity} {rule.QuantityUnit}.");
        return result;
    }
    public static decimal Quantity(LegalPermissionType rule, PermissionIntervalCalculation value) => rule.QuantityUnit == "WEEKS" ? value.Dates.Count(d => d.CountsAsPermissionDay) / 7m : value.Dates.Sum(d => Quantity(rule.QuantityUnit, value.Unit, d.CountsAsPermissionDay, d.EffectiveMinutes, d));
    private static decimal Quantity(string target, string unit, bool counts, int minutes, PermissionDateCalculation day) => !counts ? 0 : target switch
    {
        "WEEKS" => 1m / 7,
        "MINUTES" => minutes,
        "HOURS" => minutes / 60m,
        _ => unit == "DAYS" ? 1m : (decimal)minutes / LegalPermissionRules.FullMinutes(day)
    };
    public static async Task CheckAsync(TenantDbContext db, Guid employment, LegalPermissionType rule, LegalPermissionInput input, PermissionIntervalCalculation calculation, CancellationToken ct)
    {
        if (rule.EventLimit.HasValue && input.EventDate == default) throw new InvalidOperationException("Indique la fecha que identifica el evento.");
        var summary = await PermissionQuotaQueries.ReadAsync(db, employment, rule, input, calculation.StartDate, calculation.EndDate, candidate: calculation, ct: ct);
        PermissionQuotaQueries.EnsureAvailable(summary);
    }
}
