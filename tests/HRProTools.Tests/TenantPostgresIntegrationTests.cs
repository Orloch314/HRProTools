using HRProTools.Web.Data;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
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

            await using (var firstDb = Create(baseConnection!, first))
            {
                firstDb.TenantBranding.Add(new TenantBranding
                {
                    LogoData = [1, 2, 3],
                    LogoContentType = "image/png",
                    LogoFileName = "primero.png",
                    AntivirusStatus = FileScanStatuses.Clean,
                    ScannedAtUtc = DateTimeOffset.UtcNow
                });
                await firstDb.SaveChangesAsync();
            }
            await using (var persisted = Create(baseConnection!, first))
            {
                var info = await persisted.TenantBranding.AsNoTracking()
                    .Select(x => new TenantLogoInfo(x.LogoFileName, x.LogoContentType, x.LogoData.Length, x.AntivirusStatus, x.UpdatedAtUtc))
                    .SingleAsync();
                Assert.Equal(3, info.Size);
                var logo = await persisted.TenantBranding.SingleAsync();
                Assert.Equal([1, 2, 3], logo.LogoData);
                logo.LogoData = [4, 5, 6];
                logo.LogoFileName = "sustituido.png";
                await persisted.SaveChangesAsync();
            }
            await using (var isolated = Create(baseConnection!, second))
                Assert.Empty(await isolated.TenantBranding.ToListAsync());
            await using (var replaced = Create(baseConnection!, first))
            {
                var logo = await replaced.TenantBranding.SingleAsync();
                Assert.Equal([4, 5, 6], logo.LogoData);
                replaced.TenantBranding.Remove(logo);
                await replaced.SaveChangesAsync();
            }
            await using (var deleted = Create(baseConnection!, first))
                Assert.Empty(await deleted.TenantBranding.ToListAsync());
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{first}\" CASCADE; DROP SCHEMA IF EXISTS \"{second}\" CASCADE;");
        }
    }

    [Fact]
    public void LogoValidationUsesFileSignaturesAndRejectsUnsupportedContent()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0x49, 0x48, 0x44, 0x52];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0xFF, 0xD9];
        byte[] webp = [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20];

        Assert.Equal("image/png", TenantLogoFileValidator.Validate(png).ContentType);
        Assert.Equal("image/jpeg", TenantLogoFileValidator.Validate(jpeg).ContentType);
        Assert.Equal("image/webp", TenantLogoFileValidator.Validate(webp).ContentType);
        Assert.Throws<InvalidOperationException>(() => TenantLogoFileValidator.Validate("<svg/>"u8));
        Assert.Throws<InvalidOperationException>(() => TenantLogoFileValidator.Validate("MZ"u8));
        Assert.Throws<InvalidOperationException>(() => TenantLogoFileValidator.Validate(new byte[TenantLogoFileValidator.MaximumBytes + 1]));
    }

    [Fact]
    public async Task ProductionLogoUploadRequiresConfiguredAntivirus()
    {
        var environment = new TestWebHostEnvironment { EnvironmentName = "Production" };
        var scanner = new ClamAvFileSecurityScanner(Options.Create(new ClamAvOptions()), environment);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scanner.ScanAsync(new byte[] { 1, 2, 3 }));

        Assert.Contains("obligatorio", error.Message);
    }

    [Fact]
    public async Task EmployeeNumberIsUniqueAndHistoriesPreservePreviousValues()
    {
        var baseConnection = Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnection), "Defina HRPROTOOLS_TEST_CONNECTION para la prueba PostgreSQL.");
        var schema = "hr_t_" + Guid.NewGuid().ToString("N");
        var currentEmploymentId = Guid.Empty;
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
                var newDepartment = new Department { Company = company, Code = "TEC", Name = "Tecnologia" };
                var newPosition = new Position { Company = company, Code = "SUP", Name = "Supervisor" };
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
                currentEmploymentId = employment.Id;
                var previousEmployment = new Employment
                {
                    EmployeeNumber = "LEG-000",
                    Person = person,
                    Company = company,
                    StartDate = new DateOnly(2024, 1, 1),
                    EndDate = new DateOnly(2025, 12, 31),
                    Status = EmploymentStatuses.Closed,
                    ClosureReason = "Fin de contrato"
                };
                db.AddRange(company, department, position, newDepartment, newPosition, person, employment, previousEmployment);
                db.EmploymentAssignments.Add(new EmploymentAssignment
                {
                    Employment = employment,
                    Department = department,
                    Position = position,
                    ValidFrom = employment.StartDate,
                    Reason = "Alta inicial"
                });
                db.EmploymentAssignments.Add(new EmploymentAssignment
                {
                    Employment = previousEmployment,
                    Department = department,
                    Position = position,
                    ValidFrom = previousEmployment.StartDate,
                    ValidTo = previousEmployment.EndDate,
                    Reason = "Alta inicial"
                });
                db.SalaryHistory.Add(new SalaryHistory
                {
                    Employment = employment,
                    GrossSalary = 5_000_000m,
                    ValidFrom = employment.StartDate
                });
                db.SalaryHistory.Add(new SalaryHistory
                {
                    Employment = previousEmployment,
                    GrossSalary = 4_000_000m,
                    ValidFrom = previousEmployment.StartDate,
                    ValidTo = previousEmployment.EndDate
                });
                await db.SaveChangesAsync();

                var firstAssignment = await db.EmploymentAssignments.SingleAsync(x => x.EmploymentId == employment.Id && x.ValidTo == null);
                var firstSalary = await db.SalaryHistory.SingleAsync(x => x.EmploymentId == employment.Id && x.ValidTo == null);
                firstAssignment.ValidTo = new DateOnly(2026, 5, 31);
                firstSalary.ValidTo = new DateOnly(2026, 5, 31);
                db.EmploymentAssignments.Add(new EmploymentAssignment
                {
                    EmploymentId = employment.Id,
                    DepartmentId = newDepartment.Id,
                    PositionId = newPosition.Id,
                    ValidFrom = new DateOnly(2026, 6, 1),
                    Reason = "Cambio organizativo"
                });
                db.SalaryHistory.Add(new SalaryHistory
                {
                    EmploymentId = employment.Id,
                    GrossSalary = 5_500_000m,
                    ValidFrom = new DateOnly(2026, 6, 1)
                });
                await db.SaveChangesAsync();

                newDepartment.Name = "Tecnologia corregida";
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
            Assert.Equal(3, await verification.EmploymentAssignments.CountAsync());
            Assert.Equal(3, await verification.SalaryHistory.CountAsync());
            Assert.Single(await verification.EmploymentAssignments.Where(x => x.ValidTo == null).ToListAsync());
            Assert.Equal(5_500_000m, await verification.SalaryHistory.Where(x => x.ValidTo == null).Select(x => x.GrossSalary).SingleAsync());

            var profile = await TenantEmployeeHistoryQueries.GetProfileAsync(verification, currentEmploymentId);
            Assert.NotNull(profile);
            Assert.Equal(3, profile.LaborHistory.Count);
            Assert.Equal("Tecnologia corregida", profile.LaborHistory[0].Department);
            Assert.Equal("Supervisor", profile.LaborHistory[0].Position);
            Assert.True(profile.LaborHistory[0].IsCurrent);
            Assert.Equal("Cambio organizativo", profile.LaborHistory[0].Reason);
            Assert.Equal(profile.LaborHistory[0].ValidFrom.AddDays(-1), profile.LaborHistory[1].ValidTo);
            Assert.Contains(profile.LaborHistory, x => x.EmployeeNumber == "LEG-000" && !x.IsCurrent);

            var salaryHistory = await TenantEmployeeHistoryQueries.GetSalaryHistoryAsync(verification, currentEmploymentId);
            Assert.Equal(3, salaryHistory.Count);
            Assert.Equal(5_500_000m, salaryHistory[0].GrossSalary);
            Assert.True(salaryHistory[0].IsCurrent);
            Assert.Equal("PYG", salaryHistory[0].Currency);
            Assert.Equal(salaryHistory[0].ValidFrom.AddDays(-1), salaryHistory[1].ValidTo);
            Assert.Contains(salaryHistory, x => x.EmployeeNumber == "LEG-000" && !x.IsCurrent);
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;");
        }
    }

    [Fact]
    public async Task AutomaticIdentifiersAdvanceAndEmployeeListQueryRunsInPostgres()
    {
        var baseConnection = Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnection), "Defina HRPROTOOLS_TEST_CONNECTION para la prueba PostgreSQL.");
        var schema = "hr_t_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(baseConnection);
        await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\";");
            await using var db = Create(baseConnection!, schema);
            await db.Database.MigrateAsync();

            var company = new Company
            {
                Code = "EMP-0041",
                LegalName = "Empresa automatica",
                TradeName = "Empresa automatica",
                TaxId = "90000000-1"
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                Assert.Equal("EMP-0042", await TenantIdentifierAllocator.NextCompanyCodeAsync(db));
                Assert.Equal("DEP-0001", await TenantIdentifierAllocator.NextDepartmentCodeAsync(db, company.Id));
                Assert.Equal("CAR-0001", await TenantIdentifierAllocator.NextPositionCodeAsync(db, company.Id));
                await transaction.RollbackAsync();
            }

            var department = new Department { CompanyId = company.Id, Code = "DEP-0001", Name = "Administracion" };
            var position = new Position { CompanyId = company.Id, Code = "ANA", Name = "Analista" };
            var person = new Person
            {
                DocumentNumber = "4567890",
                NormalizedDocumentNumber = "4567890",
                GivenNames = "Carlos",
                FamilyNames = "Prueba"
            };
            var employment = new Employment
            {
                EmployeeNumber = "41",
                Person = person,
                CompanyId = company.Id,
                StartDate = new DateOnly(2026, 11, 18)
            };
            db.AddRange(department, position, person, employment);
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
                GrossSalary = 7_000_000m,
                ValidFrom = employment.StartDate
            });
            await db.SaveChangesAsync();

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                Assert.Equal("42", await TenantIdentifierAllocator.NextEmployeeNumberAsync(db));
                await transaction.RollbackAsync();
            }

            var rows = await TenantEmployeeQueries.ListAsync(db);
            var row = Assert.Single(rows);
            Assert.Equal("41", row.EmployeeNumber);
            Assert.Equal("Empresa automatica", row.Company);
            Assert.Equal("Administracion", row.Department);
            Assert.Equal("Analista", row.Position);
            Assert.Equal(7_000_000m, row.GrossSalary);
            Assert.Null(await db.Positions.Select(x => x.Level).SingleAsync());
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

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "HRProTools.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
