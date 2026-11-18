using System.ComponentModel.DataAnnotations;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class CompanyForm
{
    public Guid? Id { get; set; }
    [MaxLength(32)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string LegalName { get; set; } = string.Empty;
    [Required, MaxLength(32)] public string TaxId { get; set; } = string.Empty;
    [EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [MaxLength(40)] public string Phone { get; set; } = string.Empty;
    [MaxLength(400)] public string Address { get; set; } = string.Empty;
}

public sealed class CatalogForm
{
    public Guid? Id { get; set; }
    [Required] public Guid CompanyId { get; set; }
    [MaxLength(32)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Name { get; set; } = string.Empty;
    [MaxLength(80)] public string Level { get; set; } = string.Empty;
}

public sealed class EmployeeForm
{
    [Required, MaxLength(40)] public string DocumentNumber { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string GivenNames { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string FamilyNames { get; set; } = string.Empty;
    [Required] public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid DepartmentId { get; set; }
    [Required] public Guid PositionId { get; set; }
    [EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [MaxLength(40)] public string Phone { get; set; } = string.Empty;
    [Range(0.01, 999999999999)] public decimal GrossSalary { get; set; }
}

public sealed record EmployeeRow(
    Guid EmploymentId,
    string EmployeeNumber,
    string DocumentNumber,
    string GivenNames,
    string FamilyNames,
    string FullName,
    string Email,
    string Phone,
    Guid CompanyId,
    string Company,
    Guid DepartmentId,
    string Department,
    Guid PositionId,
    string Position,
    DateOnly StartDate,
    string Status,
    decimal GrossSalary);

public sealed class TenantDataService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory contextFactory)
{
    private async Task<(Tenant Tenant, TenantDbContext Db)> OpenAsync(CancellationToken cancellationToken)
    {
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        return (tenant, contextFactory.Create(tenant.SchemaName));
    }

    public async Task<List<Company>> GetCompaniesAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
            return await db.Companies.AsNoTracking()
                .Where(x => includeInactive || x.IsActive)
                .OrderBy(x => x.LegalName).ToListAsync(cancellationToken);
    }

    public async Task SaveCompanyAsync(CompanyForm form, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var item = form.Id is null
                ? new Company()
                : await db.Companies.SingleAsync(x => x.Id == form.Id, cancellationToken);
            if (form.Id is null)
                item.Code = await TenantIdentifierAllocator.NextCompanyCodeAsync(db, cancellationToken);
            item.LegalName = form.LegalName.Trim();
            item.TradeName = form.LegalName.Trim();
            item.TaxId = form.TaxId.Trim();
            item.Email = form.Email.Trim();
            item.Phone = form.Phone.Trim();
            item.Address = form.Address.Trim();
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (form.Id is null) db.Companies.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<List<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
            return await db.Departments.AsNoTracking().Include(x => x.Company)
                .OrderBy(x => x.Company.LegalName).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task SaveDepartmentAsync(CatalogForm form, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (!await db.Companies.AnyAsync(x => x.Id == form.CompanyId && x.IsActive, cancellationToken))
                throw new InvalidOperationException("La empresa no esta activa.");
            var item = form.Id is null ? new Department() :
                await db.Departments.SingleAsync(x => x.Id == form.Id, cancellationToken);
            if (form.Id is not null && item.CompanyId != form.CompanyId)
                throw new InvalidOperationException("No se puede cambiar la empresa del departamento.");
            item.CompanyId = form.CompanyId;
            if (form.Id is null)
                item.Code = await TenantIdentifierAllocator.NextDepartmentCodeAsync(db, form.CompanyId, cancellationToken);
            item.Name = form.Name.Trim();
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (form.Id is null) db.Departments.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task ToggleDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        {
            var item = await db.Departments.SingleAsync(x => x.Id == id, cancellationToken);
            if (item.IsActive && await db.EmploymentAssignments.AnyAsync(x => x.DepartmentId == id && x.ValidTo == null, cancellationToken))
                throw new InvalidOperationException("El departamento tiene funcionarios activos asignados.");
            item.IsActive = !item.IsActive;
            item.DeactivatedAtUtc = item.IsActive ? null : DateTimeOffset.UtcNow;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<Position>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
            return await db.Positions.AsNoTracking().Include(x => x.Company)
                .OrderBy(x => x.Company.LegalName).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task SavePositionAsync(CatalogForm form, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        {
            if (string.IsNullOrWhiteSpace(form.Code))
                throw new InvalidOperationException("Indique el codigo del cargo.");
            if (!await db.Companies.AnyAsync(x => x.Id == form.CompanyId && x.IsActive, cancellationToken))
                throw new InvalidOperationException("La empresa no esta activa.");
            var item = form.Id is null ? new Position() :
                await db.Positions.SingleAsync(x => x.Id == form.Id, cancellationToken);
            if (form.Id is not null && item.CompanyId != form.CompanyId)
                throw new InvalidOperationException("No se puede cambiar la empresa del cargo.");
            item.CompanyId = form.CompanyId;
            item.Code = form.Code.Trim().ToUpperInvariant();
            item.Name = form.Name.Trim();
            item.Level = form.Level.Trim();
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (form.Id is null) db.Positions.Add(item);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task TogglePositionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        {
            var item = await db.Positions.SingleAsync(x => x.Id == id, cancellationToken);
            if (item.IsActive && await db.EmploymentAssignments.AnyAsync(x => x.PositionId == id && x.ValidTo == null, cancellationToken))
                throw new InvalidOperationException("El cargo tiene funcionarios activos asignados.");
            item.IsActive = !item.IsActive;
            item.DeactivatedAtUtc = item.IsActive ? null : DateTimeOffset.UtcNow;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<EmployeeRow>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
            return await TenantEmployeeQueries.ListAsync(db, cancellationToken);
    }

    public Task<string> CreateEmployeeAsync(EmployeeForm form, CancellationToken cancellationToken = default) =>
        CreateEmployeeCoreAsync(form, null, cancellationToken);

    public Task<string> ImportEmployeeAsync(
        EmployeeForm form,
        string employeeNumber,
        CancellationToken cancellationToken = default) =>
        CreateEmployeeCoreAsync(form, employeeNumber, cancellationToken);

    private async Task<string> CreateEmployeeCoreAsync(
        EmployeeForm form,
        string? importedEmployeeNumber,
        CancellationToken cancellationToken)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            string employeeNumber;
            if (importedEmployeeNumber is null)
            {
                employeeNumber = await TenantIdentifierAllocator.NextEmployeeNumberAsync(db, cancellationToken);
            }
            else
            {
                employeeNumber = importedEmployeeNumber.Trim().ToUpperInvariant();
                if (employeeNumber.Length is 0 or > 32)
                    throw new InvalidOperationException("El numero de legajo importado no es valido.");
                await TenantIdentifierAllocator.LockEmployeeNumbersAsync(db, cancellationToken);
                if (await db.Employments.AnyAsync(x => x.EmployeeNumber == employeeNumber, cancellationToken))
                    throw new InvalidOperationException($"El legajo {employeeNumber} ya existe.");
            }

            var department = await db.Departments.AsNoTracking().SingleAsync(x => x.Id == form.DepartmentId && x.IsActive, cancellationToken);
            var position = await db.Positions.AsNoTracking().SingleAsync(x => x.Id == form.PositionId && x.IsActive, cancellationToken);
            if (department.CompanyId != form.CompanyId || position.CompanyId != form.CompanyId)
                throw new InvalidOperationException("Empresa, departamento y cargo no coinciden.");

            var normalizedDocument = NormalizeDocument(form.DocumentNumber);
            var person = await db.People.SingleOrDefaultAsync(
                x => x.DocumentType == "CI" && x.NormalizedDocumentNumber == normalizedDocument, cancellationToken);
            if (person is null)
            {
                person = new Person { DocumentType = "CI", NormalizedDocumentNumber = normalizedDocument };
                db.People.Add(person);
            }
            person.DocumentNumber = form.DocumentNumber.Trim();
            person.GivenNames = form.GivenNames.Trim();
            person.FamilyNames = form.FamilyNames.Trim();
            person.Email = form.Email.Trim();
            person.Phone = form.Phone.Trim();
            person.UpdatedAtUtc = DateTimeOffset.UtcNow;

            var employment = new Employment
            {
                EmployeeNumber = employeeNumber,
                Person = person,
                CompanyId = form.CompanyId,
                StartDate = form.StartDate
            };
            db.Employments.Add(employment);
            db.EmploymentAssignments.Add(new EmploymentAssignment
            {
                Employment = employment,
                DepartmentId = form.DepartmentId,
                PositionId = form.PositionId,
                ValidFrom = form.StartDate,
                Reason = "Alta inicial"
            });
            db.SalaryHistory.Add(new SalaryHistory
            {
                Employment = employment,
                GrossSalary = form.GrossSalary,
                ValidFrom = form.StartDate
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return employeeNumber;
        }
    }

    public async Task UpdatePersonAsync(Guid employmentId, string givenNames, string familyNames, string email, string phone, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        {
            var employment = await db.Employments.Include(x => x.Person).SingleAsync(x => x.Id == employmentId, cancellationToken);
            employment.Person.GivenNames = givenNames.Trim();
            employment.Person.FamilyNames = familyNames.Trim();
            employment.Person.Email = email.Trim();
            employment.Person.Phone = phone.Trim();
            employment.Person.UpdatedAtUtc = DateTimeOffset.UtcNow;
            employment.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ChangeAssignmentAsync(Guid employmentId, Guid departmentId, Guid positionId, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var employment = await db.Employments.SingleAsync(x => x.Id == employmentId && x.Status == EmploymentStatuses.Active, cancellationToken);
            var department = await db.Departments.SingleAsync(x => x.Id == departmentId && x.IsActive, cancellationToken);
            var position = await db.Positions.SingleAsync(x => x.Id == positionId && x.IsActive, cancellationToken);
            if (department.CompanyId != employment.CompanyId || position.CompanyId != employment.CompanyId)
                throw new InvalidOperationException("El departamento y el cargo deben pertenecer a la empresa del vinculo.");
            var current = await db.EmploymentAssignments.SingleAsync(x => x.EmploymentId == employmentId && x.ValidTo == null, cancellationToken);
            if (effectiveDate <= current.ValidFrom)
                throw new InvalidOperationException("La nueva vigencia debe ser posterior a la asignacion actual.");
            current.ValidTo = effectiveDate.AddDays(-1);
            db.EmploymentAssignments.Add(new EmploymentAssignment
            {
                EmploymentId = employmentId,
                DepartmentId = departmentId,
                PositionId = positionId,
                ValidFrom = effectiveDate,
                Reason = "Cambio organizativo"
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task ChangeSalaryAsync(Guid employmentId, decimal salary, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var current = await db.SalaryHistory.SingleAsync(x => x.EmploymentId == employmentId && x.ValidTo == null, cancellationToken);
            if (effectiveDate <= current.ValidFrom)
                throw new InvalidOperationException("La nueva vigencia salarial debe ser posterior a la actual.");
            current.ValidTo = effectiveDate.AddDays(-1);
            db.SalaryHistory.Add(new SalaryHistory { EmploymentId = employmentId, GrossSalary = salary, ValidFrom = effectiveDate });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task CloseEmploymentAsync(Guid employmentId, DateOnly endDate, string reason, CancellationToken cancellationToken = default)
    {
        var (_, db) = await OpenAsync(cancellationToken);
        await using (db)
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var employment = await db.Employments.SingleAsync(x => x.Id == employmentId && x.Status == EmploymentStatuses.Active, cancellationToken);
            if (endDate < employment.StartDate || string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Indique fecha y motivo validos para el cierre.");
            employment.Status = EmploymentStatuses.Closed;
            employment.EndDate = endDate;
            employment.ClosureReason = reason.Trim();
            employment.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var assignment = await db.EmploymentAssignments.SingleAsync(x => x.EmploymentId == employmentId && x.ValidTo == null, cancellationToken);
            assignment.ValidTo = endDate;
            var salary = await db.SalaryHistory.SingleAsync(x => x.EmploymentId == employmentId && x.ValidTo == null, cancellationToken);
            salary.ValidTo = endDate;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static string NormalizeDocument(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
