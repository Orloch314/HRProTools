using System.Security.Claims;
using ClosedXML.Excel;
using HRProTools.Web.Data;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HRProTools.Web.Security;
using Npgsql;

namespace HRProTools.Tests;

public sealed class Phase3VacationTests
{
    [Theory]
    [InlineData(1, 12)]
    [InlineData(5, 12)]
    [InlineData(6, 18)]
    [InlineData(10, 18)]
    [InlineData(11, 30)]
    public void LegalDaysRespectApprovedSeniorityBoundaries(int years, decimal expected) =>
        Assert.Equal(expected, ParaguayVacationRules.LegalDays(years));

    [Fact]
    public async Task PoliciesAreNotRetroactiveAndAccrualIsIdempotent()
    {
        var connection = Connection(); var schema = Schema();
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\"");
            await using var db = Create(connection, schema); await db.Database.MigrateAsync();
            var company = new Company { Code = "EMP", LegalName = "Empresa", TradeName = "Empresa", TaxId = Guid.NewGuid().ToString("N") };
            var person = new Person { DocumentNumber = "1", NormalizedDocumentNumber = "1", GivenNames = "Ana", FamilyNames = "Prueba" };
            var employment = new Employment { EmployeeNumber = "1", Person = person, Company = company, StartDate = new DateOnly(2020, 1, 1) };
            var firstPolicy = new VacationPolicy { Company = company, EffectiveFrom = new DateOnly(2025, 1, 1), AdditionalDaysYears1To5 = 2, AdditionalDaysYears6To10 = 3, AdditionalDaysOver10 = 4 };
            db.AddRange(company, person, employment, firstPolicy); await db.SaveChangesAsync();

            Assert.Equal(1, await VacationService.GenerateDuePeriodsAsync(db, new DateOnly(2026, 1, 1)));
            Assert.Equal(0, await VacationService.GenerateDuePeriodsAsync(db, new DateOnly(2026, 1, 1)));
            var old = await db.VacationPeriods.SingleAsync();
            Assert.Equal(18, old.LegalDays); Assert.Equal(3, old.AdditionalDays);

            db.VacationPolicies.Add(new VacationPolicy { CompanyId = company.Id, EffectiveFrom = new DateOnly(2026, 6, 1), AdditionalDaysYears1To5 = 5, AdditionalDaysYears6To10 = 6, AdditionalDaysOver10 = 7 });
            await db.SaveChangesAsync();
            Assert.Equal(1, await VacationService.GenerateDuePeriodsAsync(db, new DateOnly(2027, 1, 1)));
            Assert.Equal(3, (await db.VacationPeriods.SingleAsync(x => x.Id == old.Id)).AdditionalDays);
            Assert.Equal(6, (await db.VacationPeriods.SingleAsync(x => x.AccruedOn == new DateOnly(2027, 1, 1))).AdditionalDays);
            Assert.True(new DateOnly(2026, 8, 1) > old.UseBy);
            Assert.Equal(18, old.LegalDays); // remains pending because reservations do not consume legal days
        }
        finally { await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE"); }
    }

    [Fact]
    public async Task FifoReservationConsumptionReleaseReversalAndConcurrencyPreserveBalance()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationService>();
        var ids = await fixture.SeedVacationPeriodsAsync();

        var reservation = await service.ReserveAsync(ids.EmploymentId, 15, "request-1");
        Assert.Equal(2, reservation.Count); Assert.Equal(12, reservation[0].LegalDays); Assert.Equal(3, reservation[1].LegalDays);
        await service.ConsumeReservationAsync("request-1");
        await service.ReverseConsumptionAsync("consume:request-1");
        var releasedReservation = await service.ReserveAsync(ids.EmploymentId, 5, "request-2");
        Assert.NotEmpty(releasedReservation); await service.ReleaseReservationAsync("request-2");

        await using (var verify = Create(Connection(), fixture.Schema))
        {
            var balance = await VacationLedgerQueries.GetEmploymentBalanceAsync(verify, ids.EmploymentId);
            Assert.Equal(24, balance.Recognized); Assert.Equal(0, balance.Reserved); Assert.Equal(0, balance.Consumed); Assert.Equal(24, balance.Available);
        }

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async i =>
        {
            try { await service.ReserveAsync(ids.EmploymentId, 20, $"concurrent-{i}"); return true; }
            catch (InvalidOperationException) { return false; }
            catch (PostgresException) { return false; }
        }));
        Assert.Single(attempts, x => x);
        await using var final = Create(Connection(), fixture.Schema);
        Assert.True((await VacationLedgerQueries.GetEmploymentBalanceAsync(final, ids.EmploymentId)).Available >= 0);
    }

    [Fact]
    public void ExcelTemplateContainsTypedImportSheets()
    {
        var bytes = EmployeeExcelImportService.CreateTemplate();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(new[] { "Funcionarios", "Vacaciones" }, workbook.Worksheets.Select(x => x.Name));
        Assert.Equal("Legajo", workbook.Worksheet("Funcionarios").Cell("A1").GetString());
        Assert.Equal("Fecha de adquisición", workbook.Worksheet("Vacaciones").Cell("D1").GetString());
    }

    [Fact]
    public async Task ExcelImportIsTransactionalRejectsDuplicateAndPreservesMultiplePeriods()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await using (var db = Create(Connection(), fixture.Schema))
        {
            var company = new Company { Code = "EMP-0001", LegalName = "Empresa importación", TradeName = "Empresa importación", TaxId = Guid.NewGuid().ToString("N") };
            var department = new Department { Company = company, Code = "DEP-0001", Name = "Administración" };
            var position = new Position { Company = company, Code = "CAR-0001", Name = "Analista" };
            var schedule = new WorkSchedule { Name = "Jornada importación", StartTime = new(8, 0), EndTime = new(17, 0), BreakStart = new(12, 0), BreakEnd = new(13, 0) };
            db.AddRange(company, department, position, schedule); await db.SaveChangesAsync();
        }
        byte[] bytes;
        using (var workbook = new XLWorkbook(new MemoryStream(EmployeeExcelImportService.CreateTemplate())))
        {
            var e = workbook.Worksheet("Funcionarios");
            e.Cell("A2").Value = "LEG-900"; e.Cell("B2").Value = "900"; e.Cell("C2").Value = "Importada"; e.Cell("D2").Value = "Prueba"; e.Cell("E2").Value = new DateTime(2023, 1, 1);
            e.Cell("F2").Value = "EMP-0001"; e.Cell("G2").Value = "DEP-0001"; e.Cell("H2").Value = "CAR-0001"; e.Cell("I2").Value = "importada@example.test"; e.Cell("J2").Value = "0981000000"; e.Cell("K2").Value = 5_000_000; e.Cell("L2").Value = "Jornada importación";
            var v = workbook.Worksheet("Vacaciones");
            for (var row = 2; row <= 3; row++) { v.Cell(row, 1).Value = "LEG-900"; v.Cell(row, 2).Value = new DateTime(2021 + row, 1, 1); v.Cell(row, 3).Value = new DateTime(2021 + row, 12, 31); v.Cell(row, 4).Value = new DateTime(2022 + row, 1, 1); v.Cell(row, 5).Value = 12; v.Cell(row, 6).Value = 1; v.Cell(row, 7).Value = row - 1; v.Cell(row, 8).Value = 0; }
            using var stream = new MemoryStream(); workbook.SaveAs(stream); bytes = stream.ToArray();
        }
        var importer = fixture.Scope.ServiceProvider.GetRequiredService<EmployeeExcelImportService>();
        byte[] missingScheduleBytes;
        using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
        {
            workbook.Worksheet("Funcionarios").Cell("L2").Clear();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            missingScheduleBytes = stream.ToArray();
        }
        var missingSchedule = await importer.ValidateAsync("import-sin-jornada.xlsx", missingScheduleBytes);
        Assert.False(missingSchedule.IsValid);
        Assert.Contains(missingSchedule.Errors, x => x.Message.Contains("jornada", StringComparison.OrdinalIgnoreCase));

        var preview = await importer.ValidateAsync("import.xlsx", bytes);
        Assert.True(preview.IsValid, string.Join("; ", preview.Errors.Select(x => x.Message)));
        await importer.ConfirmAsync(preview);
        await using (var verify = Create(Connection(), fixture.Schema))
        {
            Assert.Equal(2, await verify.VacationPeriods.CountAsync());
            Assert.Equal(2, await verify.VacationMovements.CountAsync(x => x.Type == VacationMovementTypes.Credit));
            Assert.Equal(2, await verify.VacationMovements.CountAsync(x => x.Type == VacationMovementTypes.Consume));
            Assert.Single(await verify.Employments.ToListAsync());
        }
        var duplicate = await importer.ValidateAsync("import.xlsx", bytes);
        Assert.False(duplicate.IsValid);
        Assert.Contains(duplicate.Errors, x => x.Message.Contains("duplicado", StringComparison.OrdinalIgnoreCase));
    }

    private static string Connection() => Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION") ?? throw new InvalidOperationException("Defina HRPROTOOLS_TEST_CONNECTION.");
    private static string Schema() => "hr_t_" + Guid.NewGuid().ToString("N");
    private static TenantDbContext Create(string connection, string schema)
    {
        var cs = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema };
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(cs.ConnectionString).Options);
    }
    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }

    private sealed class ServiceFixture : IAsyncDisposable
    {
        public required string Schema { get; init; }
        public required Tenant Tenant { get; init; }
        public required ApplicationUser User { get; init; }
        public required ServiceProvider Provider { get; init; }
        public required AsyncServiceScope Scope { get; init; }

        public static async Task<ServiceFixture> CreateAsync()
        {
            var connection = Connection(); var schema = Phase3VacationTests.Schema();
            await using (var admin = new NpgsqlConnection(connection)) { await admin.OpenAsync(); await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\""); }
            var tenant = new Tenant { Code = $"T{Guid.NewGuid():N}"[..12], Name = "Tenant vacaciones", SchemaName = schema, Status = TenantStatuses.Active };
            var user = new ApplicationUser { Id = Guid.NewGuid().ToString("N"), UserName = $"vac-{Guid.NewGuid():N}@test.local", NormalizedUserName = $"VAC-{Guid.NewGuid():N}@TEST.LOCAL", Email = $"vac-{Guid.NewGuid():N}@test.local", NormalizedEmail = $"VAC-{Guid.NewGuid():N}@TEST.LOCAL", DisplayName = "Prueba", TenantId = tenant.Id, IsActive = true };
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(config);
            services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__ef_migrations_history", "auth")));
            services.AddSingleton<AuthenticationStateProvider>(new FixedAuthenticationStateProvider(user.Id));
            services.AddAuthorizationBuilder().AddPolicy(AppPolicies.TenantAdministration, p => p.RequireRole(AppRoles.TenantAdministrator));
            services.AddScoped<CurrentTenantService>(); services.AddSingleton<TenantDbContextFactory>(); services.AddScoped<VacationService>();
            services.AddSingleton<IFileSecurityScanner, CleanFileScanner>(); services.AddScoped<EmployeeExcelImportService>();
            var provider = services.BuildServiceProvider(); var scope = provider.CreateAsyncScope();
            var global = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); await global.Database.MigrateAsync(); global.Tenants.Add(tenant); global.Users.Add(user); await global.SaveChangesAsync();
            await using (var tenantDb = Create(connection, schema)) await tenantDb.Database.MigrateAsync();
            return new ServiceFixture { Schema = schema, Tenant = tenant, User = user, Provider = provider, Scope = scope };
        }

        public async Task<(Guid EmploymentId, Guid FirstPeriodId)> SeedVacationPeriodsAsync()
        {
            await using var db = Create(Connection(), Schema);
            var company = new Company { Code = "EMP", LegalName = "Empresa", TradeName = "Empresa", TaxId = Guid.NewGuid().ToString("N") };
            var person = new Person { DocumentNumber = "99", NormalizedDocumentNumber = "99", GivenNames = "FIFO", FamilyNames = "Prueba" };
            var employment = new Employment { EmployeeNumber = "99", Person = person, Company = company, StartDate = new DateOnly(2024, 1, 1) };
            var first = new VacationPeriod { Employment = employment, ReferenceStart = new(2024, 1, 1), ReferenceEnd = new(2024, 12, 31), AccruedOn = new(2025, 1, 1), UseBy = new(2025, 7, 1), LegalDays = 12 };
            var second = new VacationPeriod { Employment = employment, ReferenceStart = new(2025, 1, 1), ReferenceEnd = new(2025, 12, 31), AccruedOn = new(2026, 1, 1), UseBy = new(2026, 7, 1), LegalDays = 12 };
            db.AddRange(company, person, employment, first, second);
            db.VacationMovements.AddRange(
                new VacationMovement { VacationPeriod = first, Type = VacationMovementTypes.Credit, LegalDays = 12, OperationKey = "seed-1" },
                new VacationMovement { VacationPeriod = second, Type = VacationMovementTypes.Credit, LegalDays = 12, OperationKey = "seed-2" });
            await db.SaveChangesAsync(); return (employment.Id, first.Id);
        }

        public async ValueTask DisposeAsync()
        {
            var global = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); global.Users.Remove(User); global.Tenants.Remove(Tenant); await global.SaveChangesAsync();
            await Scope.DisposeAsync(); await Provider.DisposeAsync();
            await using var admin = new NpgsqlConnection(Connection()); await admin.OpenAsync(); await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{Schema}\" CASCADE");
        }
    }

    private sealed class FixedAuthenticationStateProvider(string userId) : AuthenticationStateProvider
    {
        private readonly AuthenticationState state = new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, AppRoles.TenantAdministrator)], "Test")));
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(state);
    }

    private sealed class CleanFileScanner : IFileSecurityScanner
    {
        public Task<FileScanResult> ScanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileScanResult(FileScanStatuses.Clean, DateTimeOffset.UtcNow, true));
    }
}
