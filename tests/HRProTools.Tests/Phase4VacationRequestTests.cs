using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HRProTools.Tests;

public sealed class Phase4VacationRequestTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    [Fact]
    public void IndividualHireRequiresExplicitWorkSchedule()
    {
        var form = new EmployeeForm
        {
            DocumentNumber = "1234567",
            GivenNames = "Ana",
            FamilyNames = "Prueba",
            CompanyId = Guid.NewGuid(),
            DepartmentId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            GrossSalary = 1_000_000,
            WorkScheduleId = null
        };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(form, new ValidationContext(form), results, true));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(EmployeeForm.WorkScheduleId)));
    }

    [Theory]
    [InlineData(false, false, 5)]
    [InlineData(true, false, 6)]
    [InlineData(true, true, 7)]
    public async Task CalendarCountsOnlyContractualWorkdaysIncludingSaturdayAndSunday(
        bool saturday,
        bool sunday,
        int expected)
    {
        await using var fixture = await TenantSchemaFixture.CreateAsync();
        var ids = await fixture.SeedEmploymentAsync(saturday, sunday);

        await using var db = fixture.CreateDb();
        var result = await WorkCalendarService.CalculateVacationIntervalAsync(
            db, ids.EmploymentId, Monday, Sunday);

        Assert.Equal(expected, result.DaysToReserve);
        Assert.Equal(sunday, result.Dates.Single(x => x.Date == Sunday).CountsAsVacationDay);
    }

    [Fact]
    public async Task ApplicableCompanyHolidaysAreExcluded()
    {
        await using var fixture = await TenantSchemaFixture.CreateAsync();
        var ids = await fixture.SeedEmploymentAsync(false, false);
        await using (var db = fixture.CreateDb())
        {
            db.CompanyHolidays.AddRange(
                new CompanyHoliday { CompanyId = ids.CompanyId, Date = Monday.AddDays(1), Name = "Feriado uno" },
                new CompanyHoliday { CompanyId = ids.CompanyId, Date = Monday.AddDays(3), Name = "Feriado dos" });
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateDb();
        var result = await WorkCalendarService.CalculateVacationIntervalAsync(
            verify, ids.EmploymentId, Monday, Sunday);

        Assert.Equal(3, result.DaysToReserve);
        Assert.Equal(2, result.Dates.Count(x => x.IsHoliday && !x.CountsAsVacationDay));
    }

    [Fact]
    public async Task CalendarUsesTheScheduleEffectiveOnEachDate()
    {
        await using var fixture = await TenantSchemaFixture.CreateAsync();
        var ids = await fixture.SeedEmploymentAsync(false, false, Monday, Monday.AddDays(2));
        await using (var db = fixture.CreateDb())
        {
            var second = new WorkSchedule
            {
                Name = "Martes a sábado",
                StartTime = new(8, 0),
                EndTime = new(17, 0),
                Monday = false,
                Tuesday = true,
                Wednesday = true,
                Thursday = true,
                Friday = true,
                Saturday = true,
                Sunday = false
            };
            db.WorkSchedules.Add(second);
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule
            {
                EmploymentId = ids.EmploymentId,
                WorkSchedule = second,
                ValidFrom = Monday.AddDays(3)
            });
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateDb();
        var result = await WorkCalendarService.CalculateVacationIntervalAsync(
            verify, ids.EmploymentId, Monday, Sunday);

        Assert.Equal(6, result.DaysToReserve);
        Assert.All(result.Dates.Take(3), x => Assert.Equal("Jornada de prueba", x.WorkScheduleName));
        Assert.All(result.Dates.Skip(3), x => Assert.Equal("Martes a sábado", x.WorkScheduleName));
        Assert.False(result.Dates.Single(x => x.Date == Sunday).CountsAsVacationDay);
    }

    [Fact]
    public async Task RequestSnapshotsDatesAndFifoStartsWithPartiallyConsumedOldestPeriod()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();

        var preview = await service.PreviewSelfAsync(Monday, Monday.AddDays(7));
        Assert.Equal(6, preview.DaysToReserve);
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));

        await using (var db = fixture.CreateTenantDb())
        {
            var request = await db.VacationRequests.Include(x => x.Dates).Include(x => x.Allocations)
                .SingleAsync(x => x.Id == requestId);
            Assert.Equal(8, request.Dates.Count);
            Assert.Equal(6, request.Dates.Count(x => x.CountsAsVacationDay));
            Assert.Equal(2, request.Allocations.Count);
            Assert.Equal(4, request.Allocations.Single(x => x.VacationPeriodId == ids.FirstPeriodId).LegalDays);
            Assert.Equal(2, request.Allocations.Single(x => x.VacationPeriodId == ids.SecondPeriodId).LegalDays);

            var schedule = await db.WorkSchedules.SingleAsync();
            schedule.Monday = false;
            db.CompanyHolidays.Add(new CompanyHoliday { CompanyId = ids.CompanyId, Date = Monday.AddDays(1), Name = "Feriado posterior" });
            await db.SaveChangesAsync();
        }

        await service.ApproveAsync(requestId, "Aprobada");
        await service.ApproveAsync(requestId, "Aprobada");
        await service.CancelSelfAsync(requestId);
        await service.CancelSelfAsync(requestId);

        var rejectedId = Guid.NewGuid();
        await service.CreateSelfAsync(rejectedId, Monday.AddDays(14), Monday.AddDays(21));
        await service.RejectAsync(rejectedId, "Rechazo de prueba");
        await service.RejectAsync(rejectedId, "Rechazo de prueba");

        await using var verify = fixture.CreateTenantDb();
        var original = await verify.VacationRequests.Include(x => x.Dates).SingleAsync(x => x.Id == requestId);
        Assert.Equal(VacationRequestStatuses.Cancelled, original.Status);
        Assert.Equal(6, original.RequestedDays);
        Assert.Equal(6, original.Dates.Count(x => x.CountsAsVacationDay));
        Assert.Equal(2, await verify.VacationMovements.CountAsync(x => x.OperationKey == $"consume:vacation-request:{requestId:N}"));
        Assert.Equal(2, await verify.VacationMovements.CountAsync(x => x.OperationKey == $"reversal:consume:vacation-request:{requestId:N}"));
        Assert.Equal(1, await verify.VacationMovements.CountAsync(x => x.OperationKey == $"release:vacation-request:{rejectedId:N}"));
        Assert.Equal(VacationRequestStatuses.Rejected, (await verify.VacationRequests.SingleAsync(x => x.Id == rejectedId)).Status);
        var balance = await VacationLedgerQueries.GetEmploymentBalanceAsync(verify, ids.EmploymentId);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(8, balance.Consumed);
        Assert.Equal(16, balance.Available);
        Assert.True(balance.Available >= 0);
    }

    private static string Connection() => Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Defina HRPROTOOLS_TEST_CONNECTION.");

    private static string Schema() => "hr_t_" + Guid.NewGuid().ToString("N");

    private sealed class TenantSchemaFixture : IAsyncDisposable
    {
        public required string SchemaName { get; init; }

        public static async Task<TenantSchemaFixture> CreateAsync()
        {
            var schema = Schema();
            await using var admin = new NpgsqlConnection(Connection());
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\"");
            var fixture = new TenantSchemaFixture { SchemaName = schema };
            await using var db = fixture.CreateDb();
            await db.Database.MigrateAsync();
            return fixture;
        }

        public TenantDbContext CreateDb()
        {
            var cs = new NpgsqlConnectionStringBuilder(Connection()) { SearchPath = SchemaName };
            return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(cs.ConnectionString).Options);
        }

        public async Task<(Guid EmploymentId, Guid CompanyId)> SeedEmploymentAsync(
            bool saturday,
            bool sunday,
            DateOnly? scheduleStart = null,
            DateOnly? scheduleEnd = null)
        {
            await using var db = CreateDb();
            var company = new Company { Code = "EMP", LegalName = "Empresa", TradeName = "Empresa", TaxId = Guid.NewGuid().ToString("N") };
            var person = new Person { DocumentNumber = "1", NormalizedDocumentNumber = "1", GivenNames = "Ana", FamilyNames = "Prueba" };
            var employment = new Employment { EmployeeNumber = "1", Person = person, Company = company, StartDate = new(2020, 1, 1) };
            var schedule = new WorkSchedule
            {
                Name = "Jornada de prueba",
                StartTime = new(8, 0),
                EndTime = new(17, 0),
                Saturday = saturday,
                Sunday = sunday
            };
            db.AddRange(company, person, employment, schedule);
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule
            {
                Employment = employment,
                WorkSchedule = schedule,
                ValidFrom = scheduleStart ?? employment.StartDate,
                ValidTo = scheduleEnd
            });
            await db.SaveChangesAsync();
            return (employment.Id, company.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await using var admin = new NpgsqlConnection(Connection());
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{SchemaName}\" CASCADE");
        }
    }

    private sealed class RequestServiceFixture : IAsyncDisposable
    {
        public required string SchemaName { get; init; }
        public required Tenant Tenant { get; init; }
        public required ApplicationUser User { get; init; }
        public required ServiceProvider Provider { get; init; }
        public required AsyncServiceScope Scope { get; init; }

        public static async Task<RequestServiceFixture> CreateAsync()
        {
            var schema = Schema();
            await using (var admin = new NpgsqlConnection(Connection()))
            {
                await admin.OpenAsync();
                await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\"");
            }
            var tenant = new Tenant { Code = $"T{Guid.NewGuid():N}"[..12], Name = "Tenant fase 4", SchemaName = schema, Status = TenantStatuses.Active };
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = $"phase4-{Guid.NewGuid():N}@test.local",
                NormalizedUserName = $"PHASE4-{Guid.NewGuid():N}@TEST.LOCAL",
                Email = $"phase4-{Guid.NewGuid():N}@test.local",
                NormalizedEmail = $"PHASE4-{Guid.NewGuid():N}@TEST.LOCAL",
                DisplayName = "Prueba Fase 4",
                TenantId = tenant.Id,
                IsActive = true
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = Connection() }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                Connection(), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "auth")));
            services.AddSingleton<AuthenticationStateProvider>(new FixedAuthenticationStateProvider(user.Id));
            services.AddAuthorizationBuilder()
                .AddPolicy(AppPolicies.EmployeeSelfService, policy => policy.RequireRole(AppRoles.Employee))
                .AddPolicy(AppPolicies.EmployeeRecords, policy => policy.RequireRole(AppRoles.TenantAdministrator, AppRoles.HumanResources));
            services.AddScoped<CurrentTenantService>();
            services.AddSingleton<TenantDbContextFactory>();
            services.AddScoped<VacationRequestService>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var global = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await global.Database.MigrateAsync();
            global.Tenants.Add(tenant);
            global.Users.Add(user);
            await global.SaveChangesAsync();
            await using (var tenantDb = CreateTenantDb(schema)) await tenantDb.Database.MigrateAsync();
            return new RequestServiceFixture { SchemaName = schema, Tenant = tenant, User = user, Provider = provider, Scope = scope };
        }

        public TenantDbContext CreateTenantDb() => CreateTenantDb(SchemaName);

        public async Task<(Guid EmploymentId, Guid CompanyId, Guid FirstPeriodId, Guid SecondPeriodId)> SeedAsync()
        {
            await using var db = CreateTenantDb();
            var company = new Company { Code = "EMP", LegalName = "Empresa", TradeName = "Empresa", TaxId = Guid.NewGuid().ToString("N") };
            var person = new Person { DocumentNumber = "99", NormalizedDocumentNumber = "99", GivenNames = "FIFO", FamilyNames = "Prueba" };
            var employment = new Employment { EmployeeNumber = "99", Person = person, Company = company, StartDate = new(2020, 1, 1) };
            var schedule = new WorkSchedule { Name = "Lunes a viernes", StartTime = new(8, 0), EndTime = new(17, 0) };
            var first = new VacationPeriod { Employment = employment, ReferenceStart = new(2024, 1, 1), ReferenceEnd = new(2024, 12, 31), AccruedOn = new(2025, 1, 1), UseBy = new(2025, 7, 1), LegalDays = 12 };
            var second = new VacationPeriod { Employment = employment, ReferenceStart = new(2025, 1, 1), ReferenceEnd = new(2025, 12, 31), AccruedOn = new(2026, 1, 1), UseBy = new(2026, 7, 1), LegalDays = 12 };
            db.AddRange(company, person, employment, schedule, first, second);
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule { Employment = employment, WorkSchedule = schedule, ValidFrom = employment.StartDate });
            db.VacationMovements.AddRange(
                new VacationMovement { VacationPeriod = first, Type = VacationMovementTypes.Credit, LegalDays = 12, OperationKey = "phase4-credit-1" },
                new VacationMovement { VacationPeriod = first, Type = VacationMovementTypes.Consume, LegalDays = 8, OperationKey = "phase4-used-1" },
                new VacationMovement { VacationPeriod = second, Type = VacationMovementTypes.Credit, LegalDays = 12, OperationKey = "phase4-credit-2" });
            await db.SaveChangesAsync();

            var global = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await global.Users.SingleAsync(x => x.Id == User.Id);
            user.PersonId = person.Id;
            user.ActiveEmploymentId = employment.Id;
            await global.SaveChangesAsync();
            return (employment.Id, company.Id, first.Id, second.Id);
        }

        public async ValueTask DisposeAsync()
        {
            var global = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            global.Users.Remove(User);
            global.Tenants.Remove(Tenant);
            await global.SaveChangesAsync();
            await Scope.DisposeAsync();
            await Provider.DisposeAsync();
            await using var admin = new NpgsqlConnection(Connection());
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{SchemaName}\" CASCADE");
        }

        private static TenantDbContext CreateTenantDb(string schema)
        {
            var cs = new NpgsqlConnectionStringBuilder(Connection()) { SearchPath = schema };
            return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(cs.ConnectionString).Options);
        }
    }

    private sealed class FixedAuthenticationStateProvider(string userId) : AuthenticationStateProvider
    {
        private readonly AuthenticationState state = new(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, AppRoles.Employee),
            new Claim(ClaimTypes.Role, AppRoles.TenantAdministrator)
        ], "Test")));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(state);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
