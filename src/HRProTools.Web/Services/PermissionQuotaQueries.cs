using System.Text.Json;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record PermissionQuotaBucket(string Kind, string Period, string Unit, decimal Limit, decimal Used, decimal Reserved, decimal Candidate)
{
    public decimal Available => Math.Max(0, Limit - Used - Reserved);
    public bool Exceeded => Used + Reserved + Candidate > Limit + .000001m;
    public bool Exhausted => Available <= .000001m;
}
public sealed record PermissionQuotaRequest(Guid Id, string EmployeeNumber, DateOnly Start, DateOnly End, DateTimeOffset SubmittedAt, string Unit, decimal Requested, decimal Approved, decimal Reserved, decimal Used, string Status);
public sealed record PermissionQuotaSummary(string Code, string Name, string EmployeeNumber, string PersonName, IReadOnlyList<PermissionQuotaBucket> Buckets, IReadOnlyList<PermissionQuotaRequest> Requests, bool RestrictedHistory)
{
    public bool Exhausted => Buckets.Any(x => x.Exhausted);
}

// Reads the existing daily snapshots and statuses. Shared by creation validation and authorized balances.
public static class PermissionQuotaQueries
{
    private sealed record Point(DateOnly Date, decimal Quantity);
    private sealed record Scope(string Kind, string Period, string Unit, decimal Limit, DateOnly First, DateOnly Last, bool Event = false);
    public static async Task<PermissionQuotaSummary> ReadAsync(TenantDbContext db, Guid employmentId, LegalPermissionType rule, LegalPermissionInput input, DateOnly start, DateOnly end, PermissionIntervalCalculation? candidate = null, string? selectedUnit = null, IReadOnlySet<Guid>? visibleCompanies = null, bool historical = false, CancellationToken ct = default)
    {
        if (end < start || end.DayNumber - start.DayNumber > 800) throw new InvalidOperationException("Seleccione un período válido de hasta 800 días.");
        var employment = await db.Employments.AsNoTracking().Include(x => x.Person).SingleAsync(x => x.Id == employmentId, ct);
        var requests = await db.PermissionRequests.AsNoTracking().Include(x => x.Dates).Include(x => x.Employment).Where(x => x.Employment.PersonId == employment.PersonId && x.LegalCode == rule.Code).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var scopes = new List<Scope>();
        var target = rule.Engine == "PARAMETRIC" ? rule.QuantityUnit : rule.Code is "ACOMPANAMIENTO_DISCAPACIDAD" or "LACTANCIA" or "BUSQUEDA_EMPLEO" or "CONTROL_PRENATAL" ? "MINUTES" : "DAYS";
        var from = candidate?.StartDate ?? start; var until = candidate?.EndDate ?? end;
        var dates = candidate is null ? Enumerable.Range(0, until.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).ToList() : candidate.Dates.Where(x => x.CountsAsPermissionDay).Select(x => x.Date).ToList();
        if (candidate is null && rule.Engine != "PARAMETRIC" && rule.Code is "LACTANCIA" or "BUSQUEDA_EMPLEO")
        {
            if (historical) dates = requests.SelectMany(x => x.Dates).Where(x => x.CountsAsPermissionDay && x.Date >= from && x.Date <= until).Select(x => x.Date).Distinct().Order().ToList();
            else
            {
                var calendar = await WorkCalendarService.ResolvePermissionCalendarAsync(db, employmentId, from, until, ct);
                dates = calendar.Where(x => x.CountsAsPermissionDay).Select(x => x.Date).ToList();
                if (dates.Count == 0) throw new InvalidOperationException("El período seleccionado no contiene días laborales aplicables.");
            }
        }
        decimal? annual = rule.Engine is "PARAMETRIC" or "COMPANY_BENEFIT" ? rule.AnnualLimit : rule.Code is "PAP_MAMOGRAFIA" or "PREVENCION_PROSTATA_TESTICULOS_PENE_COLON" ? LegalPermissionLimits.PreventionAnnualDays : rule.Code == "OBLIGACION_LEGAL" ? LegalPermissionLimits.ObligationYearDays : null;
        decimal? monthly = rule.Engine == "PARAMETRIC" ? rule.MonthlyLimit : rule.Code == "OBLIGACION_LEGAL" ? LegalPermissionLimits.ObligationMonthDays : null;
        decimal? perEvent = rule.Engine == "PARAMETRIC" ? rule.EventLimit : rule.Code is "MATRIMONIO" or "LUTO_FAMILIAR" ? LegalPermissionLimits.EventDays : null;
        if (annual.HasValue) foreach (var year in dates.Select(x => x.Year).Distinct()) scopes.Add(new("YEAR", $"Año {year}", target, annual.Value, new(year, 1, 1), new(year, 12, 31)));
        if(rule.Engine=="COMPANY_BENEFIT")foreach(var year in dates.Select(x=>x.Year).Distinct())scopes.Add(new("YEAR_REQUEST",$"Año {year} · disfrute único","REQUESTS",1,new(year,1,1),new(year,12,31)));
        foreach (var month in dates.Select(x => (x.Year, x.Month)).Distinct())
        {
            var first = new DateOnly(month.Year, month.Month, 1); var last = first.AddMonths(1).AddDays(-1);
            if (monthly.HasValue) scopes.Add(new("MONTH", $"Mes {month.Month:D2}/{month.Year}", target, monthly.Value, first, last));
            if (rule.Engine != "PARAMETRIC" && rule.Code == "ACOMPANAMIENTO_DISCAPACIDAD")
            {
                var calendar = await WorkCalendarService.ResolvePermissionCalendarAsync(db, employmentId, first < employment.StartDate ? employment.StartDate : first, last, ct, includeClosed: historical);
                scopes.Add(new("MONTH", $"Mes {month.Month:D2}/{month.Year} · {LegalPermissionLimits.DisabilityMonthlyFraction:P0} de jornada aplicable", "MINUTES", decimal.Floor(calendar.Sum(x => x.EffectiveMinutes) * LegalPermissionLimits.DisabilityMonthlyFraction), first, last));
            }
        }
        if (perEvent.HasValue) scopes.Add(new("EVENT", $"Evento {input.EventDate:dd/MM/yyyy}", target, perEvent.Value, DateOnly.MinValue, DateOnly.MaxValue, true));
        if (rule.Engine != "PARAMETRIC" && rule.Code is "PATERNIDAD" or "MATERNIDAD" or "ADOPCION") scopes.Add(new("EVENT", $"Evento {input.EventDate:dd/MM/yyyy}", "REQUESTS", 1, DateOnly.MinValue, DateOnly.MaxValue, true));
        if (rule.Engine != "PARAMETRIC" && rule.Code == "LACTANCIA") foreach (var date in dates) scopes.Add(new("DAY", $"Día {date:dd/MM/yyyy}", "MINUTES", LegalPermissionLimits.LactationMinutes(input), date, date));
        if (rule.Engine != "PARAMETRIC" && rule.Code == "BUSQUEDA_EMPLEO")
        {
            if ((candidate?.Unit ?? selectedUnit ?? "HOURS") == "DAYS")
            { target = "DAYS"; foreach (var monday in dates.Select(WeekStart).Distinct()) scopes.Add(new("WEEK", $"Semana {monday:dd/MM/yyyy}", target, LegalPermissionLimits.SearchWeeklyDays, monday, monday.AddDays(6))); }
            else foreach (var date in dates) scopes.Add(new("DAY", $"Día {date:dd/MM/yyyy}", "MINUTES", LegalPermissionLimits.SearchDailyMinutes, date, date));
        }
        bool SameEvent(PermissionRequest r) => r.LegalInput is not null && JsonSerializer.Deserialize<LegalPermissionInput>(r.LegalInput)?.EventDate == input.EventDate;
        List<Point> Stored(PermissionRequest r, string unit) => r.Dates.Where(d => d.CountsAsPermissionDay).Select(d => new Point(d.Date, Quantity(unit, r.Unit, d.EffectiveMinutes, d.LegalDayFraction))).ToList();
        List<Point> Proposed(string unit) => candidate?.Dates.Where(d => d.CountsAsPermissionDay).Select(d => new Point(d.Date, Quantity(unit, candidate.Unit, d.EffectiveMinutes, candidate.Unit == "DAYS" ? 1 : (decimal)d.EffectiveMinutes / LegalPermissionRules.FullMinutes(d)))).ToList() ?? [];
        decimal Amount(PermissionRequest r, Scope s) => s.Event && !SameEvent(r) ? 0 : s.Unit == "REQUESTS" ? (r.Dates.Any(x=>x.CountsAsPermissionDay&&x.Date>=s.First&&x.Date<=s.Last)?1:0) : Stored(r, s.Unit).Where(x => x.Date >= s.First && x.Date <= s.Last).Sum(x => x.Quantity);
        var buckets = scopes.Select(s => new PermissionQuotaBucket(s.Kind, s.Period, s.Unit, s.Limit,
            requests.Where(r => r.Status == "APPROVED").Sum(r => Amount(r, s)), requests.Where(r => r.Status == "PENDING").Sum(r => Amount(r, s)),
            candidate is null ? 0 : s.Unit == "REQUESTS" ? 1 : Proposed(s.Unit).Where(x => x.Date >= s.First && x.Date <= s.Last).Sum(x => x.Quantity))).ToList();
        var eventScoped = scopes.Any(x => x.Event);
        var displayFirst = scopes.Where(x => !x.Event).Select(x => x.First).DefaultIfEmpty(from).Min();
        var displayLast = scopes.Where(x => !x.Event).Select(x => x.Last).DefaultIfEmpty(until).Max();
        var rows = new List<PermissionQuotaRequest>(); var restricted = false;
        foreach (var r in requests)
        {
            if (eventScoped && !SameEvent(r)) continue;
            var points = Stored(r, target).Where(x => eventScoped || x.Date >= displayFirst && x.Date <= displayLast).ToList();
            if (points.Count == 0) continue;
            if (visibleCompanies is not null && !visibleCompanies.Contains(r.CompanyId)) { restricted = true; continue; }
            var q = points.Sum(x => x.Quantity); var approved = r.Status == "APPROVED" ? q : 0; var reserved = r.Status == "PENDING" ? q : 0;
            rows.Add(new(r.Id, r.Employment.EmployeeNumber, r.StartDate, r.EndDate, r.CreatedAtUtc, target, q, approved, reserved, approved, r.Status));
        }
        return new(rule.Code, rule.Name, employment.EmployeeNumber, employment.Person.GivenNames + " " + employment.Person.FamilyNames, buckets, rows, restricted);
    }
    public static decimal Quantity(string target, string unit, int minutes, decimal fraction) => target switch { "WEEKS" => 1m / 7, "MINUTES" => minutes, "HOURS" => minutes / 60m, _ => unit == "DAYS" ? 1 : fraction };
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static void EnsureAvailable(PermissionQuotaSummary summary)
    {
        var exceeded = summary.Buckets.FirstOrDefault(x => x.Exceeded); if (exceeded is null) return;
        var unit = exceeded.Unit == "REQUESTS" ? "solicitud(es)" : LegalDurationInfo.UnitName(exceeded.Unit).ToLowerInvariant();
        throw new InvalidOperationException($"No se puede presentar: el máximo de {exceeded.Limit:0.###} {unit} para {exceeded.Period.ToLowerInvariant()} está utilizado o reservado. Utilizados: {exceeded.Used:0.###}; reservados: {exceeded.Reserved:0.###}; disponibles: {exceeded.Available:0.###}.");
    }
}
