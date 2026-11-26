using System.Text.Json;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class PublishedPermissionRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public int Revision { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; }
    public string RuleJson { get; set; } = "";
}
public static class GlobalReferenceQueries
{
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Asuncion"));

    public static async Task<List<LegalPermissionType>> PermissionsAsync(TenantDbContext db, DateOnly date, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<PublishedPermissionRow>($"SELECT \"Id\",\"Code\",\"Revision\",\"ValidFrom\",\"ValidTo\",\"IsActive\",\"RuleJson\" FROM platform.legal_permission_version WHERE \"ValidFrom\" <= {date}").ToListAsync(ct);
        return rows.GroupBy(x => x.Code).Select(g => g.OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.Revision).First())
            .Where(x => x.IsActive && (!x.ValidTo.HasValue || x.ValidTo >= date))
            .Select(x => JsonSerializer.Deserialize<LegalPermissionType>(x.RuleJson)! with { RuleVersion = x.Id.ToString(), RuleEffectiveFrom = x.ValidFrom, RuleEffectiveTo = x.ValidTo }).OrderBy(x => x.Group).ThenBy(x => x.Name).ToList();
    }
    public static async Task<LegalPermissionType> RequiredPermissionAsync(TenantDbContext db, string code, CancellationToken ct = default) =>
        (await PermissionsAsync(db, Today, ct)).SingleOrDefault(x => x.Code == code) ?? throw new InvalidOperationException("El permiso no está activo en el catálogo global vigente para presentar solicitudes.");

    public static async Task<Dictionary<DateOnly, string>> HolidaysAsync(TenantDbContext db, Guid companyId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        // Latest revision must be selected before filtering dates, so a moved date never remains counted twice.
        var rows = new List<OfficialHolidayVersion>();
        foreach(var year in Enumerable.Range(start.Year,end.Year-start.Year+1)) rows.AddRange(await OfficialHolidayRecurrence.ReadAsync(db,year,ct));
        var official = rows.GroupBy(x => x.HolidayId).Select(g => g.MaxBy(x => x.Revision)!).Where(x => x.IsActive && x.ObservedDate >= start && x.ObservedDate <= end)
            .Select(x => (Date: x.ObservedDate, Name: x.Name));
        var additional = await db.CompanyHolidays.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive && x.Date >= start && x.Date <= end).Select(x => new { x.Date, x.Name }).ToListAsync(ct);
        return official.Concat(additional.Select(x => (x.Date, x.Name))).GroupBy(x => x.Date).ToDictionary(g => g.Key, g => string.Join(" / ", g.Select(x => x.Name).Distinct()));
    }
}
