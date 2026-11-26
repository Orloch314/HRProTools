using System.Data;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class ReferenceAdministrationService(IServiceScopeFactory scopes, AuthenticationStateProvider authentication, CurrentTenantService currentTenant)
{
    public static readonly string[] Excluded = ["TRATAMIENTO_ONCOLOGICO", "PROTECCION_VIOLENCIA", "ACCIDENTE_LABORAL", "SINDICAL", "PERMISO_CONTRACTUAL"];
    public async Task<List<LegalPermissionVersion>> GetPermissionsAsync(CancellationToken ct = default)
    {
        await GuardAsync(false, ct);
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LegalPermissionVersions.AsNoTracking().OrderBy(x => x.Code).ThenByDescending(x => x.Revision).ToListAsync(ct);
    }
    public async Task<Guid> SavePermissionAsync(LegalPermissionType rule, DateOnly from, DateOnly? to, bool active, string reason, Guid? expectedId, CancellationToken ct = default)
    {
        var user = await GuardAsync(true, ct);
        if (!Regex.IsMatch(rule.Code, "^[A-Z][A-Z0-9_]{1,63}$") || Excluded.Contains(rule.Code) || rule.Code==CompanyDayOffService.Code) throw new InvalidOperationException("Código inválido, reservado o categoría expresamente excluida.");
        if (string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 200 || string.IsNullOrWhiteSpace(rule.Law) || string.IsNullOrWhiteSpace(rule.Article) || string.IsNullOrWhiteSpace(rule.Duration) || string.IsNullOrWhiteSpace(rule.Requirements) || string.IsNullOrWhiteSpace(rule.Documentation) || string.IsNullOrWhiteSpace(rule.Remuneration) || rule.Group.Length == 0 || rule.Description.Length > 4000) throw new InvalidOperationException("Complete nombre, grupo, fundamento, duración, condiciones, documentación y régimen.");
        ValidateSource(rule.SourceUrl);
        if (string.IsNullOrWhiteSpace(rule.Description) || string.IsNullOrWhiteSpace(rule.Beneficiaries) || string.IsNullOrWhiteSpace(rule.Notice) || new[] { rule.Group, rule.Law, rule.Article, rule.Duration, rule.Beneficiaries, rule.Requirements, rule.Documentation, rule.Notice, rule.Remuneration }.Any(x => x.Length > 4000)) throw new InvalidOperationException("Complete descripción, beneficiarios y preaviso; use como máximo 4000 caracteres por campo.");
        if (to < from || reason.Trim().Length is < 5 or > 1000) throw new InvalidOperationException("Revise vigencia y motivo del cambio.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE platform.legal_permission_version IN SHARE ROW EXCLUSIVE MODE", ct);
        var previous = await db.LegalPermissionVersions.Where(x => x.Code == rule.Code).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (previous?.Id != expectedId) throw new InvalidOperationException("La versión cambió; vuelva a abrir la categoría antes de guardar.");
        var old = previous is null ? null : JsonSerializer.Deserialize<LegalPermissionType>(previous.RuleJson)!;
        if (rule.Engine == "SPECIAL")
        {
            if (old is null || old.Engine != "SPECIAL" || rule.Unit != old.Unit || rule.Duration != old.Duration || rule.Protected != old.Protected || rule.Medical != old.Medical || rule.IsPaid != old.IsPaid || rule.QuantityUnit != old.QuantityUnit || rule.MaximumQuantity != old.MaximumQuantity || rule.EventLimit != old.EventLimit || rule.MonthlyLimit != old.MonthlyLimit || rule.AnnualLimit != old.AnnualLimit)
                throw new InvalidOperationException("Esta regla especializada no admite cambiar duración, unidad, naturaleza o límites desde el panel. Requiere adaptar y probar su lógica; no se guardó una configuración incompatible.");
        }
        else
        {
            if (rule.Medical || (old is null && !rule.MaximumQuantity.HasValue)) throw new InvalidOperationException("Una categoría simple nueva necesita duración máxima numérica. Los supuestos médicos especiales requieren su lógica específica.");
            if (old is not null && (old.Unit != rule.Unit || old.QuantityUnit != rule.QuantityUnit)) throw new InvalidOperationException("Para conservar los consumos históricos, una unidad distinta requiere una categoría nueva.");
            if (rule.Engine != "PARAMETRIC" || old?.Engine == "SPECIAL" || rule.Unit is not "DAYS" and not "HOURS" and not "MINUTES" and not "WEEKS" and not "DAYS_OR_HOURS" || rule.QuantityUnit is not "DAYS" and not "HOURS" and not "MINUTES" and not "WEEKS" || rule.MaximumQuantity <= 0 || rule.EventLimit <= 0 || rule.MonthlyLimit <= 0 || rule.AnnualLimit <= 0)
                throw new InvalidOperationException("Configuración no soportada: seleccione unidad simple y límites positivos o vacíos. Las reglas especiales conservan su motor.");
            if ((rule.Unit == "DAYS_OR_HOURS" && rule.QuantityUnit != "DAYS") || (rule.Unit != "DAYS_OR_HOURS" && rule.QuantityUnit != rule.Unit)) throw new InvalidOperationException("Los límites deben usar la misma unidad; para días/horas se usan fracciones de jornada en días.");
            if (rule.Unit is "DAYS" or "WEEKS" && rule.MaximumQuantity.HasValue && rule.MaximumQuantity != decimal.Truncate(rule.MaximumQuantity.Value)) throw new InvalidOperationException("La duración máxima en días o semanas debe ser entera.");
        }
        if (previous is not null)
        {
            if (from < previous.ValidFrom) throw new InvalidOperationException("Agregue una versión desde la última vigencia o posterior; no se reescribe una versión antigua.");
            var closed = from == previous.ValidFrom ? from : from.AddDays(-1);
            if (previous.ValidTo is null || previous.ValidTo > closed) previous.ValidTo = closed;
        }
        var next = new LegalPermissionVersion { Code = rule.Code, Revision = (previous?.Revision ?? 0) + 1, ValidFrom = from, ValidTo = to, IsActive = active, ChangeReason = reason.Trim(), ChangedByUserId = user };
        next.RuleJson = JsonSerializer.Serialize(rule with { RuleVersion = next.Id.ToString(), RuleEffectiveFrom = from, RuleEffectiveTo = to });
        db.LegalPermissionVersions.Add(next);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return next.Id;
    }
    public async Task<(List<OfficialHolidayVersion> Versions, OfficialCalendarYear? Year)> GetHolidaysAsync(int year, CancellationToken ct = default)
    {
        await GuardAsync(false, ct);
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var all = await OfficialHolidayRecurrence.ReadAsync(db,year,ct);
        var ids = all.GroupBy(x => x.HolidayId).Where(g => g.Any(x => x.Year == year)).Select(g => g.Key).ToHashSet();
        return (all.Where(x => ids.Contains(x.HolidayId)).ToList(), await db.OfficialCalendarYears.AsNoTracking().SingleOrDefaultAsync(x => x.Year == year, ct));
    }
    public async Task<Guid> SaveHolidayAsync(OfficialHolidayVersion value, Guid? expectedId, CancellationToken ct = default)
    {
        var user = await GuardAsync(true, ct);
        if (value.Year != value.ObservedDate.Year || value.Year is < 1900 or > 2200 || string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 200 || string.IsNullOrWhiteSpace(value.LegalBasis) || value.LegalBasis.Length > 500 || value.ChangeReason.Trim().Length is < 5 or > 1000 || value.Disposition.Length > 500) throw new InvalidOperationException("Complete nombre, año, fecha efectiva, fundamento y motivo.");
        ValidateSource(value.SourceUrl);
        if (value.Kind is not "NATIONAL" and not "MOVED" and not "EXTRAORDINARY" || value.ChangeKind is not "INITIAL" and not "OFFICIAL_MOVE" and not "CORRECTION") throw new InvalidOperationException("Tipo de feriado o cambio inválido.");
        if ((value.ChangeKind == "OFFICIAL_MOVE" || value.Kind is "MOVED" or "EXTRAORDINARY") && string.IsNullOrWhiteSpace(value.Disposition)) throw new InvalidOperationException("Indique la disposición oficial para traslado o feriado extraordinario; una corrección interna no es un decreto.");
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE platform.official_holiday_version IN SHARE ROW EXCLUSIVE MODE", ct);
        var previous = await db.OfficialHolidayVersions.Where(x => x.HolidayId == value.HolidayId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        var calculated = previous is null ? (await OfficialHolidayRecurrence.ReadAsync(db,value.OriginalDate.Year,ct)).SingleOrDefault(x=>x.HolidayId==value.HolidayId && x.Revision==0) : null;
        if ((previous?.Id ?? calculated?.Id) != expectedId) throw new InvalidOperationException("El feriado cambió; vuelva a abrirlo.");
        if(calculated is not null && value.ChangeKind=="INITIAL") throw new InvalidOperationException("Use traslado o corrección para una recurrencia existente.");
        if (previous is not null && value.ChangeKind == "INITIAL") throw new InvalidOperationException("Distinga traslado oficial y corrección administrativa.");
        value.Id = Guid.NewGuid(); value.Revision = (previous?.Revision ?? 0) + 1; value.ChangedAtUtc = DateTimeOffset.UtcNow; value.ChangedByUserId = user;
        db.OfficialHolidayVersions.Add(value);
        var years = new[] { value.Year, previous?.Year ?? value.Year }.Distinct();
        foreach (var year in years)
        {
            var status = await db.OfficialCalendarYears.SingleOrDefaultAsync(x => x.Year == year, ct);
            if (status is null) db.OfficialCalendarYears.Add(status = new OfficialCalendarYear { Year = year });
            status.IsConfirmed = false; status.VerificationNote = "Calendario modificado: pendiente de confirmar integridad del año."; status.ChangedAtUtc = DateTimeOffset.UtcNow; status.ChangedByUserId = user;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return value.Id;
    }
    public async Task ConfirmYearAsync(int year, bool confirmed, string source, string note, CancellationToken ct = default)
    {
        var user = await GuardAsync(true, ct); ValidateSource(source);
        if (year is < 1900 or > 2200 || note.Trim().Length is < 10 or > 1000) throw new InvalidOperationException("Indique año y verificación documental del calendario completo.");
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE platform.official_holiday_version IN SHARE ROW EXCLUSIVE MODE", ct);
        var versions = await OfficialHolidayRecurrence.ReadAsync(db,year,ct);
        if (confirmed && !versions.GroupBy(x => x.HolidayId).Select(g => g.MaxBy(x => x.Revision)!).Any(x => x.Year == year && x.IsActive)) throw new InvalidOperationException("No puede confirmar un calendario vacío.");
        var status = await db.OfficialCalendarYears.SingleOrDefaultAsync(x => x.Year == year, ct);
        if (status is null) db.OfficialCalendarYears.Add(status = new OfficialCalendarYear { Year = year });
        status.IsConfirmed = confirmed; status.SourceUrl = source; status.VerificationNote = note.Trim(); status.ChangedByUserId = user; status.ChangedAtUtc = DateTimeOffset.UtcNow;
        db.OperationLogs.Add(new OperationLog { ActorUserId = user, Action = "OFFICIAL_CALENDAR_STATUS", Subject = year.ToString(), Detail = $"{(confirmed ? "CONFIRMED" : "PENDING")}: {note.Trim()}" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<string> GuardAsync(bool write, CancellationToken ct)
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        if (!principal.IsInRole(AppRoles.GlobalAdministrator) && (write || !principal.IsInRole(AppRoles.TenantAdministrator))) throw new UnauthorizedAccessException("Solo Administrador Global puede modificar catálogos oficiales.");
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Users.AnyAsync(x => x.Id == id && x.IsActive, ct)) throw new UnauthorizedAccessException("La cuenta no está activa.");
        if (!principal.IsInRole(AppRoles.GlobalAdministrator)) await currentTenant.GetRequiredAsync(ct);
        return id;
    }
    private static void ValidateSource(string source)
    {
        if (source.Length > 1500 || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0) throw new InvalidOperationException("Indique una URL HTTPS válida de la fuente normativa/documental.");
    }
}
