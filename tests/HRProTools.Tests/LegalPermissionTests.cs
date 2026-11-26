using System.ComponentModel.DataAnnotations;
using HRProTools.Web.Data;
using HRProTools.Web.Services;
using HRProTools.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;

namespace HRProTools.Tests;

public sealed partial class Phase4VacationRequestTests
{
    [Theory]
    [InlineData("M", true)]
    [InlineData("F", true)]
    [InlineData("", false)]
    [InlineData("ND", false)]
    [InlineData("X", false)]
    [InlineData(null, false)]
    public void HireRequiresControlledExplicitSex(string? sex, bool expected)
    {
        var form = new EmployeeForm { Sex = sex! };
        var result = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), result, true);
        Assert.Equal(expected, !result.Any(x => x.MemberNames.Contains(nameof(EmployeeForm.Sex))));
    }

    [Theory]
    [InlineData("MATRIMONIO", 3)]
    [InlineData("LUTO_FAMILIAR", 3)]
    [InlineData("PATERNIDAD", 14)]
    [InlineData("MATERNIDAD", 126)]
    [InlineData("ADOPCION", 126)]
    public async Task LegalCalendarLicensesPersistRuleAndRemainSelfServiceOnly(string code, int days)
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var input = new LegalPermissionInput { Code = code, EventDate = Monday, Relationship = "ABUELO", ChildBirthDate = Monday.AddMonths(-2) };
        var id = Guid.NewGuid();
        await requests.CreateSelfAsync(id, "PAID", "DAYS", Monday, Monday.AddDays(days - 1), null, null, "Registro de prueba sin información sensible", legal: input);
        Assert.Equal(id, await requests.CreateSelfAsync(id, "PAID", "DAYS", Monday, Monday.AddDays(days - 1), null, null, "Registro de prueba sin información sensible", legal: input));
        await using (var db = fixture.CreateTenantDb())
        {
            var saved = await db.PermissionRequests.Include(x => x.ApprovalSteps).SingleAsync();
            Assert.Equal(days, saved.RequestedDays); Assert.Equal(code, saved.LegalCode); Assert.Null(saved.SexAtSubmission);
            Assert.Contains(InitialGlobalReferenceSeed.Id(code).ToString(), saved.LegalSnapshot!);
            Assert.All(saved.ApprovalSteps, x => Assert.Equal(VacationApprovalLevelTypes.HumanResources, x.Type));
        }
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday, Monday, null, null, "Intento administrativo", legal: input));
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => requests.RejectAsync(id, "Rechazo discrecional no autorizado"));
        fixture.AuthenticateAs(fixture.HumanResourcesUser, AppRoles.HumanResources);
        await Assert.ThrowsAsync<InvalidOperationException>(() => requests.RejectAsync(id, "Rechazo discrecional no autorizado"));
    }

    [Fact]
    public void MaternitySelectsGreaterExtensionAndDoesNotAddSpecialAndMultiple()
    {
        Assert.Equal(Monday.AddDays(167), LegalPermissionRules.MaternityEnd(Monday, "SPECIAL", 1));
        var ordinaryMultiple = Monday.AddDays(126).AddMonths(1).AddDays(-1);
        Assert.Equal(ordinaryMultiple > Monday.AddDays(167) ? ordinaryMultiple : Monday.AddDays(167), LegalPermissionRules.MaternityEnd(Monday, "MULTIPLE_SPECIAL", 2));
    }

    [Theory]
    [InlineData("PAP_MAMOGRAFIA", "PAP")]
    [InlineData("PREVENCION_PROSTATA_TESTICULOS_PENE_COLON", "COLON")]
    public async Task PreventiveAnnualQuotaCountsPendingOnceAndCancellationReleasesIt(string code, string exam)
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); var ids = await fixture.SeedAsync();
        await using (var db = fixture.CreateTenantDb()) { var person = await db.People.SingleAsync(); person.Sex = "F"; await db.SaveChangesAsync(); }
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var input = new LegalPermissionInput { Code = code, Exam = exam }; var id = Guid.NewGuid();
        await requests.CreateSelfAsync(id, "PAID", "DAYS", Monday, Monday.AddDays(1), null, null, "Examen preventivo de prueba", legal: input);
        await Assert.ThrowsAsync<InvalidOperationException>(() => requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday.AddDays(2), Monday.AddDays(2), null, null, "Otro examen de prueba", legal: input));
        await requests.CancelSelfAsync(id, "Prueba de liberación");
        await requests.CancelSelfAsync(id, "Reintento idempotente");
        await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday.AddDays(2), Monday.AddDays(3), null, null, "Examen tras cancelación", legal: input);
        Assert.Contains("2 de 2", await fixture.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>().GetSelfQuotaAsync(code, Monday));
    }

    [Fact]
    public async Task RecurrencesPrenatalExtensionsAndNoticeUseExistingCalendar()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); var ids = await fixture.SeedAsync();
        await using var db = fixture.CreateTenantDb();
        var lactation = new LegalPermissionInput { Code = "LACTANCIA", ChildBirthDate = Monday.AddMonths(-4), ReturnDate = Monday, DailyMinutes = 90 };
        var result = await LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, lactation, "HOURS", Monday, Sunday, new(8, 0), null, default);
        Assert.Equal(5 * 90, result.RequestedMinutes); Assert.Equal(7, result.Dates.Count);
        var extended = new LegalPermissionInput { Code = "LACTANCIA", Variant = "EXTENDED", ChildBirthDate = Monday.AddMonths(-8), ReturnDate = Monday.AddMonths(-3), DailyMinutes = 60, MedicalExtension = true, MedicalCertificateDate = Monday };
        Assert.Equal(300, (await LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, extended, "HOURS", Monday, Sunday, new(8, 0), null, default)).RequestedMinutes);
        var prenatal = new LegalPermissionInput { Code = "CONTROL_PRENATAL", Urgent = true, MedicalExtension = true };
        Assert.Equal(300, (await LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, prenatal, "HOURS", Monday, Monday, new(8, 0), new(14, 0), default)).RequestedMinutes);
        prenatal.MedicalExtension = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, prenatal, "HOURS", Monday, Monday, new(8, 0), new(14, 0), default));
        var search = new LegalPermissionInput { Code = "BUSQUEDA_EMPLEO", DailyMinutes = 120 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, search, "HOURS", Monday, Sunday, new(8, 0), null, default));
        db.EmploymentNotices.Add(new EmploymentNotice { EmploymentId = ids.EmploymentId, StartDate = Monday, EndDate = Sunday, Mode = "HOURS", Reference = "Preaviso sintético verificado", VerifiedByUserId = fixture.HumanResourcesUser.Id }); await db.SaveChangesAsync();
        Assert.Equal(600, (await LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, search, "HOURS", Monday, Sunday, new(8, 0), null, default)).RequestedMinutes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, search, "DAYS", Monday, Monday, null, null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, new() { Code = "REPOSO_MEDICO" }, "DAYS", Monday, Monday, null, null, default));
    }

    [Fact]
    public async Task LegalDocumentsArePrivateAndVerifiedRightsCannotBeRejected()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); await fixture.SeedAsync();
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>(); var reviews = fixture.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>();
        var id = await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday, Monday.AddDays(1), null, null, "Examen de prueba", legal: new() { Code = "PAP_MAMOGRAFIA", Exam = "PAP" });
        await reviews.UploadAsync(id, true, "constancia.pdf", "application/pdf", "%PDF-1.7 test"u8.ToArray());
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reviews.GetAsync(id, false));
        fixture.AuthenticateAs(fixture.OutOfScopeHumanResourcesUser, AppRoles.HumanResources);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reviews.GetAsync(id, false));
        fixture.AuthenticateAs(fixture.HumanResourcesUser, AppRoles.HumanResources);
        await reviews.ReviewAsync(id, "VERIFIED", "Constancia de examen y requisitos legales verificados.", null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => requests.RejectAsync(id, "No se autoriza por conveniencia"));
        await requests.ApproveAsync(id, "");
        Assert.Equal(PermissionRequestStatuses.Approved, (await reviews.GetAsync(id, false)).Status);
    }

    [Fact]
    public async Task SexChangesRequireAdministrationAndDoNotAlterRequestSnapshots()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); var ids = await fixture.SeedAsync();
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var id = await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday, Monday, null, null, "Prueba de evento", legal: new() { Code = "MATRIMONIO", EventDate = Monday });
        var service = fixture.Scope.ServiceProvider.GetRequiredService<PersonSexService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(ids.EmploymentId, "F", "Corrección administrativa"));
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        await service.UpdateAsync(ids.EmploymentId, "F", "Corrección administrativa explícita");
        await using var db = fixture.CreateTenantDb(); Assert.Equal("F", (await db.People.SingleAsync()).Sex); Assert.Null((await db.PersonSexChanges.SingleAsync()).PreviousValue); Assert.Null((await db.PermissionRequests.SingleAsync()).SexAtSubmission);
    }

    [Fact]
    public async Task MonthlyDisabilityAndLegalObligationQuotasUseActualWorkdayFractions()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); var ids = await fixture.SeedAsync();
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var obligation = new LegalPermissionInput { Code = "OBLIGACION_LEGAL", ObligationReference = "Citación gubernativa sintética" };
        await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "HOURS", Monday, Monday, new(8, 0), new(12, 0), "Comparecencia de prueba", legal: obligation);
        await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday.AddDays(1), Monday.AddDays(1), null, null, "Comparecencia de prueba", legal: obligation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday.AddDays(2), Monday.AddDays(2), null, null, "Exceso mensual de prueba", legal: obligation));
        await using var db = fixture.CreateTenantDb();
        var companion = new LegalPermissionInput { Code = "ACOMPANAMIENTO_DISCAPACIDAD", ObligationReference = "Acreditación de cuidado" };
        var first = await LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, companion, "HOURS", Monday, Monday, new(8, 0), new(12, 0), default);
        Assert.Equal(240, first.RequestedMinutes);
        var month = await WorkCalendarService.CalculatePermissionDaysAsync(db, ids.EmploymentId, new(2026, 11, 1), new(2026, 11, 30));
        Assert.Equal(21, month.RequestedDays); // 168h monthly, half = 84h; not a fixed quota for all months.
        var request = new PermissionRequest { EmploymentId = ids.EmploymentId, CompanyId = ids.CompanyId, DepartmentId = (await db.Departments.FirstAsync()).Id, LegalCode = companion.Code, PermissionType = "PAID", Unit = "HOURS", StartDate = Monday, EndDate = Monday, StartTime = new(8, 0), EndTime = new(12, 0), RequestedMinutes = 84 * 60, Reason = "Reserva mensual sintética", RequestedByUserId = fixture.User.Id };
        request.Dates.Add(new PermissionRequestDay { Date = Monday, CountsAsPermissionDay = true, EffectiveMinutes = 84 * 60 }); db.PermissionRequests.Add(request); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalPermissionRules.CheckQuotasAsync(db, ids.EmploymentId, companion, first, default));
    }

    [Fact]
    public async Task SpecialMaternityAdoptionAndBereavementRejectUnsupportedCases()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); var ids = await fixture.SeedAsync(); await using var db = fixture.CreateTenantDb();
        var adoption = new LegalPermissionInput { Code = "ADOPCION", EventDate = Monday, ChildBirthDate = Monday.AddMonths(-9) };
        Assert.Equal(84, (await LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, adoption, "DAYS", Monday, Monday.AddDays(83), null, null, default)).RequestedDays);
        var transfer = new LegalPermissionInput { Code = "MATERNIDAD", Variant = "TRANSFER", OriginalStart = Monday, OriginalEnd = Monday.AddDays(125), DeathDate = Monday.AddDays(10) };
        Assert.Equal(116, (await LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, transfer, "DAYS", Monday.AddDays(10), Monday.AddDays(125), null, null, default)).RequestedDays);
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, transfer, "DAYS", Monday, Monday.AddDays(125), null, null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalPermissionRules.CalculateAsync(db, ids.EmploymentId, new() { Code = "LUTO_FAMILIAR", Relationship = "PRIMO" }, "DAYS", Monday, Monday.AddDays(2), null, null, default));
        Assert.DoesNotContain(LegalPermissionCatalog.All, x => new[] { "TRATAMIENTO_ONCOLOGICO", "PROTECCION_VIOLENCIA", "ACCIDENTE_LABORAL", "SINDICAL", "PERMISO_CONTRACTUAL" }.Contains(x.Code));
    }

    [Fact]
    public async Task ConcurrentPreventiveRequestsCannotExceedAnnualQuota()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); await fixture.SeedAsync();
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        async Task<bool> Try(DateOnly day) { try { await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", day, day.AddDays(1), null, null, "Concurrencia de prueba", legal: new() { Code = "PREVENCION_PROSTATA_TESTICULOS_PENE_COLON", Exam = "COLON" }); return true; } catch (InvalidOperationException) { return false; } catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001") { return false; } }
        var results = await Task.WhenAll(Try(Monday), Try(Monday.AddDays(2)));
        Assert.Single(results, x => x); await using var db = fixture.CreateTenantDb(); Assert.Equal(2, await db.PermissionRequests.SumAsync(x => x.RequestedDays));
    }

    [Fact]
    public async Task IndividualHireStoresSexOnPersonAndPreservesExistingPerson()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync(); var ids = await fixture.SeedAsync();
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        var data = fixture.Scope.ServiceProvider.GetRequiredService<TenantDataService>();
        await using var db = fixture.CreateTenantDb(); var department = await db.Departments.SingleAsync(x => x.CompanyId == ids.CompanyId); var position = await db.Positions.SingleAsync(x => x.CompanyId == ids.CompanyId); var schedule = await db.WorkSchedules.FirstAsync();
        var form = new EmployeeForm { DocumentNumber = "NEW-SEX-001", GivenNames = "Persona", FamilyNames = "Sintética", Sex = "F", CompanyId = ids.CompanyId, DepartmentId = department.Id, PositionId = position.Id, StartDate = Monday, WorkScheduleId = schedule.Id, GrossSalary = 1_000_000 };
        await data.CreateEmployeeAsync(form); Assert.Equal("F", (await db.People.SingleAsync(x => x.DocumentNumber == form.DocumentNumber)).Sex);
        form.Sex = "X"; await Assert.ThrowsAsync<InvalidOperationException>(() => data.CreateEmployeeAsync(form));
        form.Sex = null!; await Assert.ThrowsAsync<InvalidOperationException>(() => data.CreateEmployeeAsync(form));
    }

    [Fact]
    public async Task SexCorrectionConvertsOnlyLegacySentinelAndPreservesMaleFemaleAndOtherData()
    {
        var schema = Schema();
        await using (var admin = new NpgsqlConnection(Connection()))
        {
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\"");
        }
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(Connection()) { SearchPath = schema, Pooling = false };
            await using var db = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connection.ConnectionString).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261123174711_LegalPermissionsAndPersonSex");
            var legacyId = Guid.NewGuid();
            // Omit Sex: reproduce exactly the preceding migration's database default, not a user-selected value.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO person (\"Id\",\"DocumentType\",\"DocumentNumber\",\"NormalizedDocumentNumber\",\"GivenNames\",\"FamilyNames\",\"Email\",\"Phone\",\"CreatedAtUtc\",\"UpdatedAtUtc\") VALUES ({legacyId}, 'CI','LEGACY','LEGACY','Persona','Histórica','','',{DateTimeOffset.UtcNow},{DateTimeOffset.UtcNow})");
            var male = new Person { DocumentNumber = "M1", NormalizedDocumentNumber = "M1", GivenNames = "Prueba", FamilyNames = "M", Sex = "M" };
            var female = new Person { DocumentNumber = "F1", NormalizedDocumentNumber = "F1", GivenNames = "Prueba", FamilyNames = "F", Sex = "F" };
            db.People.AddRange(male, female);
            await db.SaveChangesAsync();
            var before = await db.People.AsNoTracking().ToDictionaryAsync(x => x.Id);
            Assert.Equal("ND", before[legacyId].Sex);
            await db.Database.MigrateAsync();
            var after = await db.People.AsNoTracking().ToDictionaryAsync(x => x.Id);
            Assert.Null(after[legacyId].Sex);
            Assert.Equal("M", after[male.Id].Sex);
            Assert.Equal("F", after[female.Id].Sex);
            foreach (var (id, previous) in before)
            {
                var current = after[id];
                Assert.Equal(previous.DocumentNumber, current.DocumentNumber);
                Assert.Equal(previous.GivenNames, current.GivenNames);
                Assert.Equal(previous.FamilyNames, current.FamilyNames);
                Assert.Equal(previous.CreatedAtUtc, current.CreatedAtUtc);
                Assert.Equal(previous.UpdatedAtUtc, current.UpdatedAtUtc);
            }
            foreach (var invalid in new[] { "ND", "", "X" })
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE person SET \"Sex\" = {invalid} WHERE \"Id\" = {legacyId}"));
                Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            }
            Assert.Null((await db.People.AsNoTracking().SingleAsync(x => x.Id == legacyId)).Sex);
        }
        finally
        {
            await using var admin = new NpgsqlConnection(Connection());
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"DROP SCHEMA \"{schema}\" CASCADE");
        }
    }

    [Fact]
    public async Task LegalObligationAnnualLimitAndTenantIsolationAreEnforced()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var input = new LegalPermissionInput { Code = "OBLIGACION_LEGAL", ObligationReference = "Citación gubernativa sintética" };
        Guid last = Guid.Empty;
        for (var month = 1; month <= 8; month++)
        {
            var first = new DateOnly(2026, month, 1);
            await using var calendarDb = fixture.CreateTenantDb();
            var calendar = await WorkCalendarService.CalculatePermissionDaysAsync(calendarDb, ids.EmploymentId, first, first.AddDays(14));
            var workdays = calendar.Dates.Where(x => x.CountsAsPermissionDay).Take(2).Select(x => x.Date).ToList();
            first = workdays[0]; var second = workdays[1];
            last = await requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", first, month == 8 ? first : second, null, null, "Obligación respaldada para prueba anual", legal: input);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => requests.CreateSelfAsync(Guid.NewGuid(), "PAID", "DAYS", Monday, Monday, null, null, "Exceso del límite anual", legal: input));
        await using var other = await RequestServiceFixture.CreateAsync();
        await other.SeedAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => other.Scope.ServiceProvider.GetRequiredService<LegalPermissionReviewService>().GetAsync(last, true));
    }
}
