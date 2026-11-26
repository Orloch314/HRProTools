using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record CalendarHoliday(Guid Id, DateOnly Date, string Name, string Origin, DateOnly OriginalDate, string Source, bool Coincides, string Confirmation = "");
public sealed record IntegratedCalendar(IReadOnlyList<CalendarHoliday> Days, bool Confirmed, string Verification, int AffectedRequests);

public sealed class TenantCalendarService(CurrentTenantService tenant, TenantDbContextFactory factory, AuthenticationStateProvider authentication, ReferenceAdministrationService official)
{
    public async Task<IntegratedCalendar> GetAsync(int year, Guid company, CancellationToken ct = default)
    {
        if (!(await authentication.GetAuthenticationStateAsync()).User.IsInRole(AppRoles.TenantAdministrator)) throw new UnauthorizedAccessException("Calendario administrativo reservado al Administrador Tenant.");
        if (year is < 1900 or > 2200) throw new InvalidOperationException("Año fuera de rango.");
        await using var db = factory.Create((await tenant.GetRequiredAsync(ct)).SchemaName);
        if (!await db.Companies.AnyAsync(x => x.Id == company, ct)) throw new UnauthorizedAccessException("Empresa no disponible en este grupo.");
        var published = await official.GetHolidaysAsync(year, ct);
        var national = published.Versions.GroupBy(x => x.HolidayId).Select(g => g.MaxBy(x => x.Revision)!).Where(x => x.IsActive && x.Year == year).ToList();
        var own = await db.CompanyHolidays.AsNoTracking().Where(x => x.CompanyId == company && x.Date.Year == year && x.IsActive).ToListAsync(ct);
        var days = national.Select(x => new CalendarHoliday(x.HolidayId, x.ObservedDate, x.Name, "OFFICIAL", x.OriginalDate, x.SourceUrl, own.Any(h => h.Date == x.ObservedDate),OfficialHolidayRecurrence.State(x)))
            .Concat(own.Select(x => new CalendarHoliday(x.Id, x.Date, x.Name, "COMPANY", x.Date, "", national.Any(h => h.ObservedDate == x.Date)))).OrderBy(x => x.Date).ToList();
        var dates = days.Select(x => x.Date).ToHashSet();
        var today = GlobalReferenceQueries.Today;
        // Compare only flags stored at submission. Never rewrite requests or FIFO reservations.
        var vacations = await db.VacationRequestDays.AsNoTracking().Where(x => x.VacationRequest.Employment.CompanyId == company && x.Date.Year == year && x.Date >= today && (x.VacationRequest.Status == "PENDING" || x.VacationRequest.Status == "APPROVED")).Select(x => new { Id = x.VacationRequestId, x.Date, x.IsHoliday }).ToListAsync(ct);
        var permissions = await db.PermissionRequestDays.AsNoTracking().Where(x => x.PermissionRequest.CompanyId == company && x.Date.Year == year && x.Date >= today && (x.PermissionRequest.Status == "PENDING" || x.PermissionRequest.Status == "APPROVED")).Select(x => new { Id = x.PermissionRequestId, x.Date, x.IsHoliday }).ToListAsync(ct);
        var absences = await db.AbsenceDays.AsNoTracking().Where(x => x.Absence.CompanyId == company && x.Date.Year == year && x.Date >= today && x.Absence.Status != "CANCELLED").Select(x => new { Id = x.AbsenceId, x.Date, x.IsHoliday }).ToListAsync(ct);
        var affected = vacations.Where(x => x.IsHoliday != dates.Contains(x.Date)).Select(x => x.Id).Distinct().Count() + permissions.Where(x => x.IsHoliday != dates.Contains(x.Date)).Select(x => x.Id).Distinct().Count() + absences.Where(x => x.IsHoliday != dates.Contains(x.Date)).Select(x => x.Id).Distinct().Count();
        return new(days, published.Year?.IsConfirmed == true, published.Year?.VerificationNote ?? OfficialHolidayRecurrence.PendingMessage, affected);
    }
}
