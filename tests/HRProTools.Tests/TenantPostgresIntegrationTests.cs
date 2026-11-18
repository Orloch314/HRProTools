using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRProTools.Tests;

public sealed class TenantPostgresIntegrationTests
{
    [Fact]
    public async Task TenantMigrationsPersistAndSchemasRemainIsolated()
    {
        var baseConnection = Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnection), "Defina HRPROTOOLS_TEST_CONNECTION para la prueba PostgreSQL.");
        var first = "hr_t_" + Guid.NewGuid().ToString("N");
        var second = "hr_t_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(baseConnection);
        await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, $"CREATE SCHEMA \"{first}\"; CREATE SCHEMA \"{second}\";");
            await MigrateAndInsertAsync(baseConnection!, first, "UNO");
            await MigrateAndInsertAsync(baseConnection!, second, "DOS");
            Assert.Equal("UNO", await ReadCodeAsync(baseConnection!, first));
            Assert.Equal("DOS", await ReadCodeAsync(baseConnection!, second));
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{first}\" CASCADE; DROP SCHEMA IF EXISTS \"{second}\" CASCADE;");
        }
    }

    [Fact]
    public async Task EmployeeNumberIsUniqueAndHistoriesPreservePreviousValues()
    {
        var baseConnection = Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnection), "Defina HRPROTOOLS_TEST_CONNECTION para la prueba PostgreSQL.");
        var schema = "hr_t_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(baseConnection);
        await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\";");
            await using (var db = Create(baseConnection!, schema))
            {
                await db.Database.MigrateAsync();
                var company = new Company { Code = "EMP", LegalName = "Empresa", TradeName = "Empresa", TaxId = "80000000-1" };
                var department = new Department { Company = company, Code = "ADM", Name = "Administracion" };
                var position = new Position { Company = company, Code = "ANA", Name = "Analista" };
                var person = new Person
                {
                    DocumentNumber = "1234567",
                    NormalizedDocumentNumber = "1234567",
                    GivenNames = "Ana",
                    FamilyNames = "Prueba"
                };
                var employment = new Employment
                {
                    EmployeeNumber = "LEG-001",
                    Person = person,
                    Company = company,
                    StartDate = new DateOnly(2026, 1, 1)
                };
                db.AddRange(company, department, position, person, employment);
                db.EmploymentAssignments.Add(new EmploymentAssignment
                {
                    Employment = employment,
                    Department = department,
                    Position = position,
                    ValidFrom = employment.StartDate
                });
                db.SalaryHistory.Add(new SalaryHistory
                {
                    Employment = employment,
                    GrossSalary = 5_000_000m,
                    ValidFrom = employment.StartDate
                });
                await db.SaveChangesAsync();

                var firstAssignment = await db.EmploymentAssignments.SingleAsync();
                var firstSalary = await db.SalaryHistory.SingleAsync();
                firstAssignment.ValidTo = new DateOnly(2026, 5, 31);
                firstSalary.ValidTo = new DateOnly(2026, 5, 31);
                db.EmploymentAssignments.Add(new EmploymentAssignment
                {
                    EmploymentId = employment.Id,
                    DepartmentId = department.Id,
                    PositionId = position.Id,
                    ValidFrom = new DateOnly(2026, 6, 1),
                    Reason = "Cambio efectivo"
                });
                db.SalaryHistory.Add(new SalaryHistory
                {
                    EmploymentId = employment.Id,
                    GrossSalary = 5_500_000m,
                    ValidFrom = new DateOnly(2026, 6, 1)
                });
                await db.SaveChangesAsync();
            }

            await using (var duplicateDb = Create(baseConnection!, schema))
            {
                var companyId = await duplicateDb.Companies.Select(x => x.Id).SingleAsync();
                var person = new Person
                {
                    DocumentNumber = "7654321",
                    NormalizedDocumentNumber = "7654321",
                    GivenNames = "Bea",
                    FamilyNames = "Prueba"
                };
                duplicateDb.Employments.Add(new Employment
                {
                    EmployeeNumber = "LEG-001",
                    Person = person,
                    CompanyId = companyId,
                    StartDate = new DateOnly(2026, 2, 1)
                });
                await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
            }

            await using var verification = Create(baseConnection!, schema);
            Assert.Equal(2, await verification.EmploymentAssignments.CountAsync());
            Assert.Equal(2, await verification.SalaryHistory.CountAsync());
            Assert.Single(await verification.EmploymentAssignments.Where(x => x.ValidTo == null).ToListAsync());
            Assert.Equal(5_500_000m, await verification.SalaryHistory.Where(x => x.ValidTo == null).Select(x => x.GrossSalary).SingleAsync());
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;");
        }
    }

    private static async Task MigrateAndInsertAsync(string baseConnection, string schema, string code)
    {
        await using var db = Create(baseConnection, schema);
        await db.Database.MigrateAsync();
        db.Companies.Add(new Company { Code = code, LegalName = code, TradeName = code, TaxId = code });
        await db.SaveChangesAsync();
    }

    private static async Task<string> ReadCodeAsync(string baseConnection, string schema)
    {
        await using var db = Create(baseConnection, schema);
        return await db.Companies.Select(x => x.Code).SingleAsync();
    }

    private static TenantDbContext Create(string baseConnection, string schema)
    {
        var connection = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema };
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connection.ConnectionString).Options;
        return new TenantDbContext(options);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
