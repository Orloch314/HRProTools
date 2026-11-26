using System.Text.Json;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRProTools.Tests;

public sealed partial class Phase4VacationRequestTests
{
    private static LegalPermissionType TestRule(string unit = "DAYS") => new("QA_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), "Permiso sintético", "Pruebas", "Norma sintética", "1", "Hasta tres unidades", unit, "Funcionarios de prueba", "Verificar el supuesto", "Constancia", "Según norma", "Con goce", false)
    { Engine = "PARAMETRIC", SourceUrl = "https://example.org/norma-sintetica", Description = "Categoría sintética de integración", QuantityUnit = unit, MaximumQuantity = 3, AnnualLimit = 5 };
    private static OfficialHolidayVersion TestHoliday(DateOnly date) => new() { Name = "Feriado sintético " + Guid.NewGuid().ToString("N"), Year = date.Year, OriginalDate = date, ObservedDate = date, LegalBasis = "Norma sintética de prueba", SourceUrl = "https://example.org/feriado-sintetico", ChangeReason = "Alta sintética controlada" };
    private static OfficialHolidayVersion Revision(OfficialHolidayVersion previous, DateOnly date, string change = "CORRECTION") => new() { HolidayId = previous.HolidayId, Name = previous.Name, Year = date.Year, OriginalDate = previous.OriginalDate, ObservedDate = date, LegalBasis = previous.LegalBasis, SourceUrl = previous.SourceUrl, ChangeReason = "Cambio sintético documentado", ChangeKind = change, Kind = change == "OFFICIAL_MOVE" ? "MOVED" : previous.Kind, Disposition = change == "OFFICIAL_MOVE" ? "Decreto sintético" : previous.Disposition };
    private static async Task CleanReferencesAsync(RequestServiceFixture f, string? code = null, Guid? holiday = null, int? year = null)
    {
        await using var scope = f.Provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (code is not null) await db.LegalPermissionVersions.Where(x => x.Code == code).ExecuteDeleteAsync();
        if (holiday.HasValue) await db.OfficialHolidayVersions.Where(x => x.HolidayId == holiday).ExecuteDeleteAsync();
        if (year.HasValue) await db.OfficialCalendarYears.Where(x => x.Year == year).ExecuteDeleteAsync();
        await db.OperationLogs.Where(x => x.ActorUserId == f.AdministratorUser.Id && x.Action == "OFFICIAL_CALENDAR_STATUS").ExecuteDeleteAsync();
    }

    [Fact]
    public async Task GlobalCatalogWritesAreGuardedAndVersionSnapshotsSurviveDeactivation()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync();
        var admin = f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>(); var requests = f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>(); var rule = TestRule();
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.SavePermissionAsync(rule, GlobalReferenceQueries.Today, null, true, "Alta de prueba", null));
            f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.SavePermissionAsync(rule, GlobalReferenceQueries.Today, null, true, "Alta de prueba", null));
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator);
            var first = await admin.SavePermissionAsync(rule, GlobalReferenceQueries.Today, null, true, "Alta de prueba", null);
            f.AuthenticateAs(f.User, AppRoles.Employee);
            Assert.Contains(await requests.GetAvailableLegalTypesAsync(), x => x.Code == rule.Code);
            var input = new LegalPermissionInput { Code = rule.Code }; var id = Guid.NewGuid();
            await requests.CreateSelfAsync(id, "PAID", "DAYS", Monday, Monday, null, null, "Solicitud sintética", legal: input);
            f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.SavePermissionAsync(rule with { Name = "Cambio no autorizado" }, GlobalReferenceQueries.Today, null, true, "Cambio de prueba", first));
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator);
            var edited = await admin.SavePermissionAsync(rule with { Name = "Nuevo nombre", MaximumQuantity = 1 }, GlobalReferenceQueries.Today, null, true, "Cambio documentado", first);
            f.AuthenticateAs(f.User, AppRoles.Employee);
            await Assert.ThrowsAsync<InvalidOperationException>(() => requests.PreviewSelfAsync("DAYS", Monday.AddDays(7), Monday.AddDays(8), null, null, legal: input));
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator);
            var second = await admin.SavePermissionAsync(rule with { Name = "Nuevo nombre", MaximumQuantity = 1 }, GlobalReferenceQueries.Today, null, false, "Baja documentada", edited);
            Assert.NotEqual(first, second); Assert.Equal(3, (await admin.GetPermissionsAsync()).Count(x => x.Code == rule.Code));
            f.AuthenticateAs(f.User, AppRoles.Employee);
            Assert.DoesNotContain(await requests.GetAvailableLegalTypesAsync(), x => x.Code == rule.Code);
            Assert.Equal(id, await requests.CreateSelfAsync(id, "PAID", "DAYS", Monday, Monday, null, null, "Solicitud sintética", legal: input));
            await Assert.ThrowsAsync<InvalidOperationException>(() => requests.PreviewSelfAsync("DAYS", Monday.AddDays(1), Monday.AddDays(1), null, null, legal: input));
            await using var db = f.CreateTenantDb(); var saved = await db.PermissionRequests.SingleAsync(x => x.Id == id); var snapshot = JsonSerializer.Deserialize<LegalPermissionType>(saved.LegalSnapshot!)!;
            Assert.Equal(first.ToString(), snapshot.RuleVersion); Assert.Equal(rule.Name, snapshot.Name); Assert.Equal(3, snapshot.MaximumQuantity); Assert.Equal(rule.Name, (await requests.GetSelfRequestsAsync()).Single().LegalName);
        }
        finally { await CleanReferencesAsync(f, code: rule.Code); }
    }

    [Fact]
    public async Task CatalogRejectsUnsupportedSpecialRulesAndResolvesFutureValidity()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync(); f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); var admin = f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>(); var rule = TestRule();
        try
        {
            var special = (await admin.GetPermissionsAsync()).Single(x => x.Code == "MATERNIDAD" && x.Revision == 1); var maternity = JsonSerializer.Deserialize<LegalPermissionType>(special.RuleJson)!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SavePermissionAsync(maternity with { Duration = "Dos días" }, GlobalReferenceQueries.Today, null, true, "Cambio incompatible", special.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SavePermissionAsync(rule with { Code = "SINDICAL" }, GlobalReferenceQueries.Today, null, true, "Categoría excluida", null));
            await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SavePermissionAsync(rule with { Unit = "RECURRENT" }, GlobalReferenceQueries.Today, null, true, "Motor no soportado", null));
            var start = GlobalReferenceQueries.Today.AddDays(5); var id = await admin.SavePermissionAsync(rule, start, start.AddDays(4), true, "Vigencia futura", null);
            await using var db = f.CreateTenantDb(); Assert.DoesNotContain(await GlobalReferenceQueries.PermissionsAsync(db, start.AddDays(-1)), x => x.Code == rule.Code); Assert.Contains(await GlobalReferenceQueries.PermissionsAsync(db, start), x => x.RuleVersion == id.ToString()); Assert.DoesNotContain(await GlobalReferenceQueries.PermissionsAsync(db, start.AddDays(5)), x => x.Code == rule.Code);
            await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SavePermissionAsync(rule with { Unit = "HOURS", QuantityUnit = "HOURS" }, start, null, true, "Cambiar unidad histórica", id));
        }
        finally { await CleanReferencesAsync(f, code: rule.Code); }
    }

    [Theory]
    [InlineData("DAYS", 2)]
    [InlineData("HOURS", 1)]
    [InlineData("MINUTES", 60)]
    [InlineData("WEEKS", 1)]
    public async Task PublishedSimpleUnitsAndCapsAreApplied(string unit, decimal quantity)
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync(); var rule = TestRule(unit) with { MaximumQuantity = quantity, AnnualLimit = quantity, MonthlyLimit = quantity, EventLimit = quantity }; var admin = f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>(); var requests = f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        try
        {
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); await admin.SavePermissionAsync(rule, GlobalReferenceQueries.Today, null, true, "Alta parametrizada", null);
            f.AuthenticateAs(f.User, AppRoles.Employee); var isTime = unit is "HOURS" or "MINUTES"; var end = unit == "WEEKS" ? Monday.AddDays(6) : unit == "DAYS" ? Monday.AddDays(1) : Monday;
            var input = new LegalPermissionInput { Code = rule.Code, EventDate = Monday }; var id = await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", isTime ? "HOURS" : "DAYS", Monday, end, isTime ? new TimeOnly(8, 0) : null, isTime ? new TimeOnly(9, 0) : null, "Registro parametrizado", legal: input);
            await Assert.ThrowsAsync<InvalidOperationException>(() => requests.PreviewSelfAsync(isTime ? "HOURS" : "DAYS", Monday.AddDays(7), end.AddDays(7), isTime ? new TimeOnly(8, 0) : null, isTime ? new TimeOnly(9, 0) : null, legal: input));
            await requests.CancelSelfAsync(id, "Liberar cupo sintético");
            Assert.NotNull(await requests.PreviewSelfAsync(isTime ? "HOURS" : "DAYS", Monday.AddDays(7), end.AddDays(7), isTime ? new TimeOnly(8, 0) : null, isTime ? new TimeOnly(9, 0) : null, legal: input));
        }
        finally { await CleanReferencesAsync(f, code: rule.Code); }
    }

    [Fact]
    public async Task OfficialFixedHolidayCanMoveWithHistoryAndTenantCannotWrite()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync(); var admin = f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>(); var calendar = f.Scope.ServiceProvider.GetRequiredService<TenantCalendarService>(); var day = TestHoliday(new(2088, 1, 1));
        try
        {
            f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.SaveHolidayAsync(day, null));
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); var first = await admin.SaveHolidayAsync(day, null);
            await admin.ConfirmYearAsync(day.Year, true, day.SourceUrl, "Calendario sintético revisado para la prueba.");
            Assert.True((await admin.GetHolidaysAsync(day.Year)).Year!.IsConfirmed);
            var moved = Revision(day, day.ObservedDate.AddDays(1), "OFFICIAL_MOVE"); var second = await admin.SaveHolidayAsync(moved, first);
            var correction = Revision(moved, moved.ObservedDate.AddDays(1)); await admin.SaveHolidayAsync(correction, second);
            var all = await admin.GetHolidaysAsync(day.Year); Assert.Equal(3, all.Versions.Count(x => x.HolidayId == day.HolidayId)); Assert.False(all.Year!.IsConfirmed);
            Assert.All(all.Versions.Where(x => x.HolidayId == day.HolidayId), x => { Assert.Equal(new DateOnly(2088, 1, 1), x.OriginalDate); Assert.Equal(day.SourceUrl, x.SourceUrl); Assert.NotEmpty(x.ChangeReason); });
            Assert.Contains(all.Versions, x => x.ChangeKind == "OFFICIAL_MOVE"); Assert.Contains(all.Versions, x => x.ChangeKind == "CORRECTION");
            f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator);
            var view = await calendar.GetAsync(day.Year, ids.CompanyId); Assert.Equal(correction.ObservedDate, Assert.Single(view.Days,x=>x.Id==day.HolidayId).Date);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.SaveHolidayAsync(Revision(correction, correction.ObservedDate.AddDays(1)), correction.Id));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.ConfirmYearAsync(day.Year, true, day.SourceUrl, "Intento tenant no autorizado"));
            await using var db = f.CreateTenantDb(); var effective = await GlobalReferenceQueries.HolidaysAsync(db, ids.CompanyId, day.ObservedDate, correction.ObservedDate); Assert.Contains(correction.ObservedDate, effective.Keys); Assert.DoesNotContain(day.Name,effective.GetValueOrDefault(day.ObservedDate)??"");
        }
        finally { await CleanReferencesAsync(f, holiday: day.HolidayId, year: day.Year); }
    }

    [Fact]
    public async Task AdditionalHolidaysAreLogicalScopedDeduplicatedAndPreserveSnapshots()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync(); await using var other = await RequestServiceFixture.CreateAsync(); var otherIds = await other.SeedAsync();
        var work = f.Scope.ServiceProvider.GetRequiredService<WorkCalendarService>(); var admin = f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>(); var calendar = f.Scope.ServiceProvider.GetRequiredService<TenantCalendarService>(); var date = new DateOnly(2089, 2, 7); while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1); var day = TestHoliday(date);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => work.SaveHolidayAsync(ids.CompanyId, day.ObservedDate, "Adicional de prueba"));
            f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator); await work.SaveHolidayAsync(ids.CompanyId, day.ObservedDate, "Adicional de prueba");
            var additional = (await work.GetHolidaysAsync()).Single();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => work.SaveHolidayAsync(otherIds.CompanyId, day.ObservedDate, "Empresa ajena"));
            f.AuthenticateAs(f.User, AppRoles.Employee);
            var requestId = await f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>().CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", date, date.AddDays(1), null, null, "Calendario empresarial previo");
            string snapshot; await using (var db = f.CreateTenantDb()) { snapshot = JsonSerializer.Serialize(await db.PermissionRequestDays.AsNoTracking().OrderBy(x => x.Date).ToListAsync()); Assert.Equal(1, (await db.PermissionRequests.SingleAsync()).RequestedDays); }
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); await admin.SaveHolidayAsync(day, null);
            f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator); var view = await calendar.GetAsync(day.Year, ids.CompanyId); Assert.Equal(2, view.Days.Count(x=>x.Date==date)); Assert.All(view.Days.Where(x=>x.Date==date), x => Assert.True(x.Coincides));
            await using (var db = f.CreateTenantDb()) { Assert.Single(await GlobalReferenceQueries.HolidaysAsync(db, ids.CompanyId, day.ObservedDate, day.ObservedDate)); }
            other.AuthenticateAs(other.AdministratorUser, AppRoles.TenantAdministrator); var otherView = await other.Scope.ServiceProvider.GetRequiredService<TenantCalendarService>().GetAsync(day.Year, otherIds.CompanyId); Assert.Equal("OFFICIAL", Assert.Single(otherView.Days,x=>x.Date==date).Origin);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => other.Scope.ServiceProvider.GetRequiredService<WorkCalendarService>().RemoveHolidayAsync(additional.Id, "Intento entre tenants"));
            await work.RemoveHolidayAsync(additional.Id, "Corrección de adicional erróneo");
            await using (var db = f.CreateTenantDb()) { var stored = await db.CompanyHolidays.SingleAsync(); Assert.False(stored.IsActive); Assert.NotNull(stored.RemovedAtUtc); Assert.Single(await GlobalReferenceQueries.HolidaysAsync(db, ids.CompanyId, day.ObservedDate, day.ObservedDate)); Assert.Equal(snapshot, JsonSerializer.Serialize(await db.PermissionRequestDays.AsNoTracking().OrderBy(x => x.Date).ToListAsync())); }
            Assert.Single((await calendar.GetAsync(day.Year, ids.CompanyId)).Days,x=>x.Date==date);
        }
        finally { await CleanReferencesAsync(f, holiday: day.HolidayId, year: day.Year); }
    }

    [Fact]
    public async Task CalendarChangesAffectNewCalculationsButNeverExistingSnapshotsOrFifo()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync(); var admin = f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>(); var requests = f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>(); var vacation = f.Scope.ServiceProvider.GetRequiredService<VacationRequestService>(); var absence = f.Scope.ServiceProvider.GetRequiredService<AbsenceService>();
        var start = new DateOnly(2091, 2, 5); while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(1); var day = TestHoliday(start);
        try
        {
            await vacation.CreateSelfAsync(Guid.NewGuid(), start, start.AddDays(1));
            await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", start.AddDays(2), start.AddDays(2), null, null, "Instantánea previa");
            await absence.CreateSelfAsync(start.AddDays(3), start.AddDays(3), AbsenceTypes.OtherJustified, "Ausencia sintética previa");
            string before; await using (var db = f.CreateTenantDb()) { before = JsonSerializer.Serialize(new { Dates = await db.VacationRequestDays.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Permissions = await db.PermissionRequestDays.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Absences = await db.AbsenceDays.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Moves = await db.VacationMovements.AsNoTracking().OrderBy(x => x.Id).ToListAsync() }); }
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); var first = await admin.SaveHolidayAsync(day, null);
            await using (var db = f.CreateTenantDb())
            {
                Assert.Equal(1, (await WorkCalendarService.CalculateVacationIntervalAsync(db, ids.EmploymentId, start, start.AddDays(1))).DaysToReserve);
                Assert.Equal(1, (await WorkCalendarService.CalculatePermissionDaysAsync(db, ids.EmploymentId, start, start.AddDays(1))).RequestedDays);
                await Assert.ThrowsAsync<InvalidOperationException>(() => WorkCalendarService.CalculatePermissionHoursAsync(db, ids.EmploymentId, start, new(8, 0), new(9, 0)));
            }
            f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator); Assert.Equal(1, (await f.Scope.ServiceProvider.GetRequiredService<TenantCalendarService>().GetAsync(start.Year, ids.CompanyId)).AffectedRequests);
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); var second = await admin.SaveHolidayAsync(Revision(day, start.AddDays(2)), first);
            await using (var db = f.CreateTenantDb())
            {
                Assert.Equal(2, (await WorkCalendarService.CalculateVacationIntervalAsync(db, ids.EmploymentId, start, start.AddDays(1))).DaysToReserve);
                Assert.Equal(1, (await WorkCalendarService.CalculatePermissionDaysAsync(db, ids.EmploymentId, start.AddDays(1), start.AddDays(2))).RequestedDays);
                var after = JsonSerializer.Serialize(new { Dates = await db.VacationRequestDays.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Permissions = await db.PermissionRequestDays.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Absences = await db.AbsenceDays.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Moves = await db.VacationMovements.AsNoTracking().OrderBy(x => x.Id).ToListAsync() }); Assert.Equal(before, after);
            }
            f.AuthenticateAs(f.User, AppRoles.Employee);
            await Assert.ThrowsAsync<InvalidOperationException>(() => requests.PreviewSelfAsync("DAYS", start.AddDays(2), start.AddDays(3), null, null));
            await using var fresh = await RequestServiceFixture.CreateAsync(); await fresh.SeedAsync();
            var newId = await fresh.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>().CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", start.AddDays(1), start.AddDays(2), null, null, "Nueva solicitud tras cambio oficial");
            await using var freshDb = fresh.CreateTenantDb(); Assert.Equal(1, (await freshDb.PermissionRequests.SingleAsync(x => x.Id == newId)).RequestedDays);
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); await admin.SaveHolidayAsync(Revision(day, start.AddDays(3)), second);
            var absenceId = await fresh.Scope.ServiceProvider.GetRequiredService<AbsenceService>().CreateSelfAsync(start.AddDays(3), start.AddDays(4), AbsenceTypes.OtherJustified, "Nueva ausencia tras cambio oficial");
            Assert.Equal(1, (await freshDb.Absences.SingleAsync(x => x.Id == absenceId)).AffectedWorkdays);
            Assert.True((await freshDb.AbsenceDays.SingleAsync(x => x.AbsenceId == absenceId && x.Date == start.AddDays(3))).IsHoliday);
        }
        finally { await CleanReferencesAsync(f, holiday: day.HolidayId, year: day.Year); }
    }

    [Fact]
    public async Task AdministrationPagesRenderAndDeclareCorrectRoutePolicies()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync();
        static void Policy<T>(string policy) { var attribute = Assert.Single(typeof(T).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()); Assert.Equal(policy, attribute.Policy); }
        Policy<HRProTools.Web.Components.Pages.AdminLegalPermissions>(AppPolicies.GlobalAdministration);
        Policy<HRProTools.Web.Components.Pages.AdminOfficialHolidays>(AppPolicies.GlobalAdministration);
        Policy<HRProTools.Web.Components.Pages.TenantLegalPermissions>(AppPolicies.TenantAdministration);
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(f.Scope.ServiceProvider, f.Scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var legal = await renderer.RenderComponentAsync<HRProTools.Web.Components.Pages.AdminLegalPermissions>(); Assert.Contains("Maternidad", legal.ToHtmlString()); Assert.Contains("Nuevo permiso", legal.ToHtmlString());
            var official = await renderer.RenderComponentAsync<HRProTools.Web.Components.Pages.AdminOfficialHolidays>(); Assert.Contains("Fecha efectiva", official.ToHtmlString()); Assert.Contains("pendiente", official.ToHtmlString());
        });
        f.AuthenticateAs(f.AdministratorUser, AppRoles.TenantAdministrator);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var legal = await renderer.RenderComponentAsync<HRProTools.Web.Components.Pages.TenantLegalPermissions>(); Assert.DoesNotContain("Publicar versión", legal.ToHtmlString());
            var integrated = await renderer.RenderComponentAsync<HRProTools.Web.Components.Shared.TenantIntegratedCalendar>(); Assert.Contains("Calendario integrado", integrated.ToHtmlString()); Assert.Contains("Feriados adicionales", integrated.ToHtmlString());
            var policies = await renderer.RenderComponentAsync<HRProTools.Web.Components.Pages.TenantVacationPolicies>();
            var html=System.Net.WebUtility.HtmlDecode(policies.ToHtmlString());Assert.Contains("Beneficio de día libre por política empresarial",html);Assert.Contains("Política de vacaciones",html);Assert.Contains("Media jornada libre",html);Assert.Contains("Una jornada completa libre",html);
        });
    }
}
