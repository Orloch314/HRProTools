using System.Text.Json;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRProTools.Tests;

public sealed partial class Phase4VacationRequestTests
{
    private static async Task AuditResolveAsync(RequestServiceFixture f, Guid id, bool approved)
    {
        f.AuthenticateAs(f.HumanResourcesUser, AppRoles.HumanResources);
        var reviews = f.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>();
        await reviews.ReviewAsync(id, approved ? "VERIFIED" : "NOT_MET", approved ? "Requisitos sintéticos verificados con plazo documental expresamente registrado." : "La documentación sintética no acredita los requisitos legales del supuesto.", approved ? GlobalReferenceQueries.Today.AddDays(30) : null);
        var requests = f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        if (approved) { await requests.ApproveAsync(id, "Verificación sintética"); await requests.ApproveAsync(id, "Reintento de prueba"); }
        else { await requests.RejectAsync(id, "Requisitos no acreditados en la prueba"); await requests.RejectAsync(id, "Reintento de prueba"); }
        f.AuthenticateAs(f.User, AppRoles.Employee);
    }
    private static Task<Guid> AuditCreateAsync(RequestServiceFixture f, LegalPermissionInput input, DateOnly start, DateOnly end, string unit = "DAYS", TimeOnly? from = null, TimeOnly? to = null) => f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>().CreateSelfAsync(Guid.NewGuid(), "PAID", unit, start, end, from, to, "Auditoría sintética de cupos", legal: input);
    private static Task<PermissionQuotaSummary> AuditBalanceAsync(RequestServiceFixture f, LegalPermissionInput input, DateOnly start, DateOnly? end = null, string unit = "DAYS") => f.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>().GetSelfQuotaDetailAsync(input, start, end ?? start, unit);

    [Theory]
    [InlineData("PAP_MAMOGRAFIA", "PAP", "MAMOGRAFIA")]
    [InlineData("PREVENCION_PROSTATA_TESTICULOS_PENE_COLON", "COLON", "PROSTATA")]
    public async Task QuotaAuditPartialApprovalsExcessPendingRejectionAndCancellation(string code, string firstExam, string secondExam)
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync();
        await using (var db = f.CreateTenantDb()) { var person = await db.People.SingleAsync(); person.Sex = "F"; await db.SaveChangesAsync(); }
        var input = new LegalPermissionInput { Code = code, Exam = firstExam };
        var first = await AuditCreateAsync(f, input, Monday, Monday); await AuditResolveAsync(f, first, true);
        var a = Assert.Single((await AuditBalanceAsync(f, input, Monday)).Buckets); Assert.Equal(2, a.Limit); Assert.Equal(1, a.Used); Assert.Equal(0, a.Reserved); Assert.Equal(1, a.Available);
        input = input with { Exam = secondExam }; var second = await AuditCreateAsync(f, input, Monday.AddDays(1), Monday.AddDays(1));
        var pending = Assert.Single((await AuditBalanceAsync(f, input, Monday)).Buckets); Assert.Equal(1, pending.Used); Assert.Equal(1, pending.Reserved); Assert.Equal(0, pending.Available);
        await AuditResolveAsync(f, second, true);
        var full = await AuditBalanceAsync(f, input, Monday); Assert.True(full.Exhausted); Assert.Equal(2, Assert.Single(full.Buckets).Used); Assert.All(full.Requests, x => { Assert.Equal(1, x.Approved); Assert.Equal(0, x.Reserved); Assert.Equal(x.Approved, x.Used); });
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, Monday.AddDays(2), Monday.AddDays(2))); Assert.Contains("máximo de 2", failure.Message);
        await using (var db = f.CreateTenantDb()) { Assert.Equal(2, await db.PermissionRequests.CountAsync()); Assert.Equal(2, await db.PermissionRequestDays.CountAsync()); }
        var requests = f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>(); await requests.CancelSelfAsync(first, "Anulación sintética"); await requests.CancelSelfAsync(first, "Reintento");
        Assert.Equal(1, Assert.Single((await AuditBalanceAsync(f, input, Monday)).Buckets).Available);
        var third = await AuditCreateAsync(f, input, Monday.AddDays(2), Monday.AddDays(2)); await AuditResolveAsync(f, third, false);
        var released = await AuditBalanceAsync(f, input, Monday); Assert.Equal(1, Assert.Single(released.Buckets).Used); Assert.Equal(0, Assert.Single(released.Buckets).Reserved); Assert.Equal(1, Assert.Single(released.Buckets).Available); Assert.Equal(3, released.Requests.Count);
        Assert.Contains(released.Requests, x => x.Status == "CANCELLED" && x.Used == 0 && x.Reserved == 0); Assert.Contains(released.Requests, x => x.Status == "REJECTED" && x.Used == 0 && x.Reserved == 0);
        var other = new LegalPermissionInput { Code = code == "PAP_MAMOGRAFIA" ? "PREVENCION_PROSTATA_TESTICULOS_PENE_COLON" : "PAP_MAMOGRAFIA", Exam = code == "PAP_MAMOGRAFIA" ? "COLON" : "PAP" };
        Assert.Equal(2, Assert.Single((await AuditBalanceAsync(f, other, Monday)).Buckets).Available);
        await AuditCreateAsync(f, other, Monday.AddDays(3), Monday.AddDays(3)); Assert.Equal(1, Assert.Single((await AuditBalanceAsync(f, input, Monday)).Buckets).Used);
    }

    [Theory]
    [InlineData("PAP_MAMOGRAFIA", "PAP")]
    [InlineData("PREVENCION_PROSTATA_TESTICULOS_PENE_COLON", "COLON")]
    public async Task QuotaAuditTwoFullConcurrentReservationsAcceptExactlyOne(string code, string exam)
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync(); var input = new LegalPermissionInput { Code = code, Exam = exam };
        async Task<bool> Send(DateOnly start) { try { await AuditCreateAsync(f, input, start, start.AddDays(1)); return true; } catch (InvalidOperationException) { return false; } }
        var accepted = await Task.WhenAll(Send(Monday), Send(Monday.AddDays(2))); Assert.Single(accepted, x => x);
        var bucket = Assert.Single((await AuditBalanceAsync(f, input, Monday)).Buckets); Assert.Equal(2, bucket.Reserved); Assert.Equal(0, bucket.Used); Assert.Equal(0, bucket.Available);
        await using var db = f.CreateTenantDb(); Assert.Equal(1, await db.PermissionRequests.CountAsync()); Assert.Equal(2, await db.PermissionRequestDays.CountAsync());
        var pendingId=await db.PermissionRequests.Select(x=>x.Id).SingleAsync();await AuditResolveAsync(f,pendingId,false);
        Assert.Equal(2,Assert.Single((await AuditBalanceAsync(f,input,Monday)).Buckets).Available);
        var approvedId=await AuditCreateAsync(f,input,Monday.AddDays(7),Monday.AddDays(8));await AuditResolveAsync(f,approvedId,true);
        Assert.Equal(2,Assert.Single((await AuditBalanceAsync(f,input,Monday)).Buckets).Used);
        var requests=f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();await requests.CancelSelfAsync(approvedId,"Anulación aprobada sintética");await requests.CancelSelfAsync(approvedId,"Reintento idempotente");
        var final=Assert.Single((await AuditBalanceAsync(f,input,Monday)).Buckets);Assert.Equal(2,final.Available);Assert.Equal(0,final.Used);Assert.Equal(0,final.Reserved);
    }

    private static async Task<Guid> AuditEmploymentAsync(RequestServiceFixture f, Guid company, Guid person, DateOnly start, string number, bool reentry)
    {
        await using var db = f.CreateTenantDb();
        if (reentry) { var old = await db.Employments.SingleAsync(x => x.PersonId == person && x.Status == EmploymentStatuses.Active); old.Status = EmploymentStatuses.Closed; old.EndDate = start.AddDays(-1); }
        var employment = new Employment { PersonId = person, CompanyId = company, StartDate = start, EmployeeNumber = number }; db.Employments.Add(employment);
        var department = await db.Departments.SingleAsync(x => x.CompanyId == company); var position = await db.Positions.SingleAsync(x => x.CompanyId == company); var schedule = await db.WorkSchedules.FirstAsync();
        db.EmploymentAssignments.Add(new() { Employment = employment, DepartmentId = department.Id, PositionId = position.Id, ValidFrom = start }); db.EmploymentWorkSchedules.Add(new() { Employment = employment, WorkScheduleId = schedule.Id, ValidFrom = start }); await db.SaveChangesAsync(); return employment.Id;
    }

    [Fact]
    public async Task QuotaAuditSplitsYearsAndDoesNotResetForNewLegajo()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync(); var input = new LegalPermissionInput { Code = "PAP_MAMOGRAFIA", Exam = "PAP" };
        await using (var db = f.CreateTenantDb()) { db.CompanyHolidays.Add(new() { CompanyId = ids.CompanyId, Date = new(2027, 1, 1), Name = "Feriado sintético de frontera anual" }); await db.SaveChangesAsync(); }
        await AuditCreateAsync(f, input, new(2026, 12, 31), new(2027, 1, 4));
        var split = await AuditBalanceAsync(f, input, new(2026, 12, 31), new DateOnly(2027, 1, 4)); Assert.Equal(2, split.Buckets.Count); Assert.All(split.Buckets, b => { Assert.Equal(1, b.Reserved); Assert.Equal(1, b.Available); });
        await AuditCreateAsync(f, input, new(2026, 12, 30), new(2026, 12, 30)); await AuditCreateAsync(f, input, new(2027, 1, 5), new(2027, 1, 5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, new(2026, 12, 29), new(2026, 12, 29)));
        Guid person; await using (var db = f.CreateTenantDb()) person = await db.Employments.Where(x => x.Id == ids.EmploymentId).Select(x => x.PersonId).SingleAsync();
        var reentry = await AuditEmploymentAsync(f, ids.CompanyId, person, new(2027, 1, 6), "100", true);
        var global = f.Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var user = await global.Users.SingleAsync(x => x.Id == f.User.Id); user.ActiveEmploymentId = reentry; await global.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, new(2027, 1, 6), new(2027, 1, 6)));
        var balance = await AuditBalanceAsync(f, input, new(2027, 1, 6)); Assert.Equal("100", balance.EmployeeNumber); Assert.Equal(2, Assert.Single(balance.Buckets).Reserved); Assert.All(balance.Requests, x => Assert.Equal("99", x.EmployeeNumber));
        Assert.Equal(new DateOnly(2026, 12, 31), DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(new DateTimeOffset(2027, 1, 1, 1, 30, 0, TimeSpan.Zero), "America/Asuncion").DateTime));
        Assert.Equal(new DateOnly(2027, 1, 1), DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(new DateTimeOffset(2027, 1, 1, 4, 30, 0, TimeSpan.Zero), "America/Asuncion").DateTime));
    }

    [Theory]
    [InlineData("MATRIMONIO")]
    [InlineData("LUTO_FAMILIAR")]
    public async Task QuotaAuditEventLimitAggregatesPartialRequests(string code)
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync(); var input = new LegalPermissionInput { Code = code, EventDate = Monday, Relationship = "ABUELO" };
        await AuditCreateAsync(f, input, Monday, Monday); await AuditCreateAsync(f, input, Monday.AddDays(1), Monday.AddDays(2));
        Assert.Equal(3, Assert.Single((await AuditBalanceAsync(f, input, Monday)).Buckets).Reserved);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, Monday.AddDays(3), Monday.AddDays(3)));
        await AuditCreateAsync(f, input with { EventDate = Monday.AddDays(7) }, Monday.AddDays(7), Monday.AddDays(7)); Assert.Equal(2, Assert.Single((await AuditBalanceAsync(f, input with { EventDate = Monday.AddDays(7) }, Monday.AddDays(7))).Buckets).Available);
    }

    [Fact]
    public async Task QuotaAuditObligationSharesHourFractionsAndSeparatesMonths()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync(); var input = new LegalPermissionInput { Code = "OBLIGACION_LEGAL", ObligationReference = "Citación sintética verificada" };
        await AuditCreateAsync(f, input, Monday, Monday, "HOURS", new(8, 0), new(12, 0));
        await AuditCreateAsync(f, input, Monday, Monday, "HOURS", new(13, 0), new(17, 0));
        await AuditCreateAsync(f, input, Monday.AddDays(1), Monday.AddDays(1));
        var balance = await AuditBalanceAsync(f, input, Monday); var monthly = Assert.Single(balance.Buckets, x => x.Kind == "MONTH"); var annual = Assert.Single(balance.Buckets, x => x.Kind == "YEAR");
        Assert.Equal(2, monthly.Reserved); Assert.Equal(0, monthly.Available); Assert.Equal(2, annual.Reserved); Assert.Equal(13, annual.Available);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, Monday.AddDays(2), Monday.AddDays(2), "HOURS", new(8, 0), new(9, 0)));
        await AuditCreateAsync(f, input, new(2026, 12, 1), new(2026, 12, 1));
        var december = await AuditBalanceAsync(f, input, new(2026, 12, 1)); Assert.Equal(1, Assert.Single(december.Buckets, x => x.Kind == "MONTH").Reserved); Assert.Equal(3, Assert.Single(december.Buckets, x => x.Kind == "YEAR").Reserved);
        await using var db = f.CreateTenantDb(); Assert.Equal(new[] { .5m, .5m, 1m, 1m }, await db.PermissionRequestDays.OrderBy(x => x.Date).ThenBy(x => x.IntervalStart).Select(x => x.LegalDayFraction).ToArrayAsync());
    }

    [Fact]
    public async Task QuotaAuditDisabilityUsesVariableMonthlyScheduleAndPreventsHourSplits()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync();
        await using (var db = f.CreateTenantDb()) { var schedule = await db.WorkSchedules.SingleAsync(); schedule.Tuesday = false; schedule.Wednesday = false; schedule.Thursday = false; schedule.Friday = false; await db.SaveChangesAsync(); }
        var input = new LegalPermissionInput { Code = "ACOMPANAMIENTO_DISCAPACIDAD", ObligationReference = "Atención sintética acreditada" };
        var first = await AuditCreateAsync(f, input, Monday, Monday, "HOURS", new(8, 0), new(17, 0)); await AuditCreateAsync(f, input, Monday.AddDays(7), Monday.AddDays(7), "HOURS", new(8, 0), new(17, 0)); await AuditCreateAsync(f, input, Monday.AddDays(14), Monday.AddDays(14), "HOURS", new(8, 0), new(12, 0));
        var month = Assert.Single((await AuditBalanceAsync(f, input, Monday, unit: "HOURS")).Buckets); Assert.Equal(20 * 60, month.Limit); Assert.Equal(20 * 60, month.Reserved); Assert.Equal(0, month.Available);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, Monday.AddDays(14), Monday.AddDays(14), "HOURS", new(13, 0), new(14, 0)));
        await using (var db = f.CreateTenantDb()) { db.CompanyHolidays.Add(new() { CompanyId = ids.CompanyId, Date = new(2026, 12, 7), Name = "Descanso sintético empresarial" }); await db.SaveChangesAsync(); }
        var december = Assert.Single((await AuditBalanceAsync(f, input, new(2026, 12, 14), unit: "HOURS")).Buckets); Assert.Equal(12 * 60, december.Limit); Assert.Equal(12 * 60, december.Available);
        await using (var db = f.CreateTenantDb()) { var employment = await db.Employments.SingleAsync(x => x.Id == ids.EmploymentId); employment.Status = EmploymentStatuses.Closed; employment.EndDate = Monday.AddDays(28); await db.SaveChangesAsync(); }
        f.AuthenticateAs(f.HumanResourcesUser, AppRoles.HumanResources);
        var historical = await f.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>().GetRequestQuotaAsync(first, false); Assert.Equal(20 * 60, Assert.Single(historical!.Buckets).Reserved);
    }

    [Fact]
    public async Task QuotaAuditDailyLactationAndSearchDoNotAllowNonOverlappingDuplicates()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync(); var requests = f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var lactation = new LegalPermissionInput { Code = "LACTANCIA", ChildBirthDate = Monday.AddMonths(-4), ReturnDate = Monday, DailyMinutes = 90 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditBalanceAsync(f, lactation, Sunday, unit: "HOURS"));
        var id = await AuditCreateAsync(f, lactation, Monday, Monday, "HOURS", new(8, 0));
        Assert.Equal(0, Assert.Single((await AuditBalanceAsync(f, lactation, Monday, unit: "HOURS")).Buckets).Available);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, lactation, Monday, Monday, "HOURS", new(14, 0)));
        await requests.CancelSelfAsync(id, "Corregir franja sintética"); await AuditCreateAsync(f, lactation, Monday, Monday, "HOURS", new(14, 0));
        var extended = lactation with { Variant = "EXTENDED", Children = 2, DailyMinutes = 120, MedicalExtension = true, MedicalCertificateDate = Monday }; await AuditCreateAsync(f, extended, Monday.AddDays(1), Monday.AddDays(1), "HOURS", new(8, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, extended, Monday.AddDays(1), Monday.AddDays(1), "HOURS", new(14, 0)));
        f.AuthenticateAs(f.HumanResourcesUser, AppRoles.HumanResources); await f.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>().RegisterNoticeAsync(ids.EmploymentId, Monday, Sunday, "HOURS", "Preaviso sintético notificado"); f.AuthenticateAs(f.User, AppRoles.Employee);
        var search = new LegalPermissionInput { Code = "BUSQUEDA_EMPLEO", DailyMinutes = 120 }; await AuditCreateAsync(f, search, Monday, Monday, "HOURS", new(8, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, search, Monday, Monday, "HOURS", new(10, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, search, Monday.AddDays(2), Monday.AddDays(2)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, search, Monday.AddDays(7), Monday.AddDays(7), "HOURS", new(8, 0)));
        f.AuthenticateAs(f.HumanResourcesUser, AppRoles.HumanResources); await f.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>().RegisterNoticeAsync(ids.EmploymentId, Monday.AddDays(7), Sunday.AddDays(7), "DAYS", "Nuevo período sintético notificado"); f.AuthenticateAs(f.User, AppRoles.Employee);
        await AuditCreateAsync(f, search, Monday.AddDays(7), Monday.AddDays(7)); await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, search, Monday.AddDays(8), Monday.AddDays(8)));
    }

    [Fact]
    public async Task QuotaAuditPrenatalDurationAndCertifiedExtensionArePersisted()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync(); var input = new LegalPermissionInput { Code = "CONTROL_PRENATAL", Urgent = true };
        await AuditCreateAsync(f, input, Monday, Monday, "HOURS", new(8, 0), new(12, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, Monday.AddDays(1), Monday.AddDays(1), "HOURS", new(8, 0), new(14, 0)));
        await AuditCreateAsync(f, input with { MedicalExtension = true }, Monday.AddDays(1), Monday.AddDays(1), "HOURS", new(8, 0), new(14, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input with { Urgent = false }, Monday.AddDays(2), Monday.AddDays(2), "HOURS", new(8, 0), new(9, 0)));
        await using var db = f.CreateTenantDb(); Assert.Equal(new[] { 240, 300 }, await db.PermissionRequests.OrderBy(x => x.StartDate).Select(x => x.RequestedMinutes).ToArrayAsync());
    }

    [Fact]
    public async Task QuotaAuditBalancesDoNotMixPeopleTenantsOrHrScopes()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); var ids = await f.SeedAsync(); var input = new LegalPermissionInput { Code = "PAP_MAMOGRAFIA", Exam = "PAP" }; var id = await AuditCreateAsync(f, input, Monday, Monday.AddDays(1));
        var reviews = f.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>();
        f.AuthenticateAs(f.OutOfScopeHumanResourcesUser, AppRoles.HumanResources); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reviews.GetRequestQuotaAsync(id, false));
        f.AuthenticateAs(f.ApproverUser, AppRoles.Approver); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reviews.GetRequestQuotaAsync(id, false));
        f.AuthenticateAs(f.HumanResourcesUser, AppRoles.HumanResources); Assert.Equal(2, Assert.Single((await reviews.GetRequestQuotaAsync(id, false))!.Buckets).Reserved);
        Guid person; await using (var db = f.CreateTenantDb()) { var other = new Person { DocumentNumber = "AUDIT-OTHER", NormalizedDocumentNumber = "AUDITOTHER", GivenNames = "Persona", FamilyNames = "Sintética distinta", Sex = "F" }; db.People.Add(other); await db.SaveChangesAsync(); person = other.Id; }
        var otherEmployment = await AuditEmploymentAsync(f, ids.CompanyId, person, new(2020, 1, 1), "101", false); var global = f.Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var otherUser = await global.Users.SingleAsync(x => x.Id == f.ApproverUser.Id); otherUser.PersonId = person; otherUser.ActiveEmploymentId = otherEmployment; await global.SaveChangesAsync();
        f.AuthenticateAs(f.ApproverUser, AppRoles.Employee); var separate = await AuditBalanceAsync(f, input, Monday); Assert.Equal(2, Assert.Single(separate.Buckets).Available); Assert.Empty(separate.Requests); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reviews.GetRequestQuotaAsync(id, true));
        await using var tenant = await RequestServiceFixture.CreateAsync(); await tenant.SeedAsync(); Assert.Equal(2, Assert.Single((await AuditBalanceAsync(tenant, input, Monday)).Buckets).Available); Assert.Empty((await AuditBalanceAsync(tenant, input, Monday)).Requests);
    }

    [Fact]
    public async Task QuotaAuditPublishedLimitChangesNeverRewriteHistoricalRequests()
    {
        await using var f = await RequestServiceFixture.CreateAsync(); await f.SeedAsync(); var rule = TestRule() with { MaximumQuantity = 2, AnnualLimit = 2 }; var admin = f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>();
        try
        {
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); var oldVersion = await admin.SavePermissionAsync(rule, GlobalReferenceQueries.Today, null, true, "Publicación sintética inicial", null);
            f.AuthenticateAs(f.User, AppRoles.Employee); var input = new LegalPermissionInput { Code = rule.Code }; var id = await AuditCreateAsync(f, input, Monday, Monday.AddDays(1));
            string snapshot; await using (var db = f.CreateTenantDb()) snapshot = (await db.PermissionRequests.SingleAsync()).LegalSnapshot!;
            f.AuthenticateAs(f.AdministratorUser, AppRoles.GlobalAdministrator); await admin.SavePermissionAsync(rule with { AnnualLimit = 1 }, GlobalReferenceQueries.Today, null, true, "Cambio sintético de límite", oldVersion);
            f.AuthenticateAs(f.User, AppRoles.Employee); var current = await AuditBalanceAsync(f, input, Monday); Assert.Equal(1, Assert.Single(current.Buckets).Limit); Assert.Equal(2, Assert.Single(current.Buckets).Reserved); Assert.Equal(0, Assert.Single(current.Buckets).Available);
            await Assert.ThrowsAsync<InvalidOperationException>(() => AuditCreateAsync(f, input, Monday.AddDays(2), Monday.AddDays(2)));
            await using (var db = f.CreateTenantDb()) { var saved = await db.PermissionRequests.SingleAsync(); Assert.Equal(snapshot, saved.LegalSnapshot); Assert.Equal(2, saved.RequestedDays); Assert.Contains(oldVersion.ToString(), saved.LegalSnapshot!); }
            await f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>().CancelSelfAsync(id, "Baja sintética válida"); Assert.Equal(1, Assert.Single((await AuditBalanceAsync(f, input, Monday)).Buckets).Available);
        }
        finally { await CleanReferencesAsync(f, code: rule.Code); }
    }
}
