using System.Security.Cryptography;
using ClosedXML.Excel;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using HRProTools.Web.Security;

namespace HRProTools.Web.Services;

public sealed record ImportValidationError(string Sheet, int Row, string Message);
public sealed record EmployeeImportRow(string EmployeeNumber, string DocumentNumber, string GivenNames, string FamilyNames, DateOnly StartDate, Guid CompanyId, Guid DepartmentId, Guid PositionId, string Email, string Phone, decimal GrossSalary, Guid WorkScheduleId);
public sealed record VacationImportRow(string EmployeeNumber, DateOnly ReferenceStart, DateOnly ReferenceEnd, DateOnly AccruedOn, decimal LegalDays, decimal AdditionalDays, decimal UsedLegalDays, decimal UsedAdditionalDays);
public sealed record EmployeeImportPreview(byte[] FileContent, string FileName, string Sha256, string AntivirusStatus, IReadOnlyList<EmployeeImportRow> Employees, IReadOnlyList<VacationImportRow> Vacations, IReadOnlyList<ImportValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0 && Employees.Count > 0;
}

public sealed class EmployeeExcelImportService(CurrentTenantService currentTenant, TenantDbContextFactory factory, IFileSecurityScanner scanner, AuthenticationStateProvider authenticationStateProvider, IAuthorizationService authorizationService)
{
    public const int MaximumBytes = 10 * 1024 * 1024;

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var employees = workbook.Worksheets.Add("Funcionarios");
        var employeeHeaders = new[] { "Legajo", "Cédula", "Nombres", "Apellidos", "Fecha de ingreso", "Empresa", "Departamento", "Cargo", "Email", "Celular", "Salario bruto", "Jornada laboral" };
        for (var i = 0; i < employeeHeaders.Length; i++) employees.Cell(1, i + 1).Value = employeeHeaders[i];
        StyleSheet(employees, employeeHeaders.Length);
        employees.Column(5).Style.DateFormat.Format = "dd/mm/yyyy";
        employees.Column(11).Style.NumberFormat.Format = "#,##0";
        employees.SheetView.FreezeRows(1);

        var vacations = workbook.Worksheets.Add("Vacaciones");
        var vacationHeaders = new[] { "Legajo", "Período desde", "Período hasta", "Fecha de adquisición", "Días legales reconocidos", "Días adicionales reconocidos", "Días legales utilizados anteriormente", "Días adicionales utilizados anteriormente" };
        for (var i = 0; i < vacationHeaders.Length; i++) vacations.Cell(1, i + 1).Value = vacationHeaders[i];
        StyleSheet(vacations, vacationHeaders.Length);
        vacations.Columns(2, 4).Style.DateFormat.Format = "dd/mm/yyyy";
        vacations.Columns(5, 8).Style.NumberFormat.Format = "0.00";
        vacations.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<EmployeeImportPreview> ValidateAsync(string fileName, byte[] content, CancellationToken ct = default)
    {
        await EnsureTenantAdministratorAsync();
        if (content.Length == 0 || content.Length > MaximumBytes) throw new InvalidOperationException("El archivo Excel no puede superar 10 MB.");
        var scan = await scanner.ScanAsync(content, ct);
        var tenant = await currentTenant.GetRequiredAsync(ct);
        await using var db = factory.Create(tenant.SchemaName);
        var errors = new List<ImportValidationError>();
        var employees = new List<EmployeeImportRow>();
        var vacations = new List<VacationImportRow>();
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var workbook = new XLWorkbook(stream);
            var employeeSheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "Funcionarios");
            var vacationSheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "Vacaciones");
            if (employeeSheet is null) errors.Add(new("Funcionarios", 0, "Falta la hoja Funcionarios."));
            if (vacationSheet is null) errors.Add(new("Vacaciones", 0, "Falta la hoja Vacaciones."));
            if (employeeSheet is not null) await ParseEmployeesAsync(db, employeeSheet, employees, errors, ct);
            if (vacationSheet is not null) ParseVacations(vacationSheet, employees, vacations, errors);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add(new("Archivo", 0, "El archivo no es un libro Excel válido: " + ex.Message));
        }
        return new(content, Path.GetFileName(fileName), Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), scan.Status, employees, vacations, errors);
    }

    public async Task<Guid> ConfirmAsync(EmployeeImportPreview preview, CancellationToken ct = default)
    {
        await EnsureTenantAdministratorAsync();
        if (!preview.IsValid) throw new InvalidOperationException("Corrija todos los errores antes de confirmar la importación.");
        var tenant = await currentTenant.GetRequiredAsync(ct);
        await using var db = factory.Create(tenant.SchemaName);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TenantIdentifierAllocator.LockEmployeeNumbersAsync(db, ct);
        foreach (var row in preview.Employees)
        {
            if (await db.Employments.AnyAsync(x => x.EmployeeNumber == row.EmployeeNumber, ct)) throw new InvalidOperationException($"El legajo {row.EmployeeNumber} ya existe.");
            var normalized = NormalizeDocument(row.DocumentNumber);
            var person = await db.People.SingleOrDefaultAsync(x => x.DocumentType == "CI" && x.NormalizedDocumentNumber == normalized, ct);
            if (person is null)
            {
                person = new Person { DocumentType = "CI", DocumentNumber = row.DocumentNumber, NormalizedDocumentNumber = normalized };
                db.People.Add(person);
            }
            person.GivenNames = row.GivenNames; person.FamilyNames = row.FamilyNames; person.Email = row.Email; person.Phone = row.Phone; person.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var employment = new Employment { EmployeeNumber = row.EmployeeNumber, Person = person, CompanyId = row.CompanyId, StartDate = row.StartDate };
            db.Employments.Add(employment);
            db.EmploymentAssignments.Add(new EmploymentAssignment { Employment = employment, DepartmentId = row.DepartmentId, PositionId = row.PositionId, ValidFrom = row.StartDate, Reason = "Importación inicial" });
            db.SalaryHistory.Add(new SalaryHistory { Employment = employment, GrossSalary = row.GrossSalary, ValidFrom = row.StartDate });
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule { Employment = employment, WorkScheduleId = row.WorkScheduleId, ValidFrom = row.StartDate });
            foreach (var vacation in preview.Vacations.Where(x => x.EmployeeNumber == row.EmployeeNumber))
            {
                var period = new VacationPeriod { Employment = employment, ReferenceStart = vacation.ReferenceStart, ReferenceEnd = vacation.ReferenceEnd, AccruedOn = vacation.AccruedOn, UseBy = vacation.AccruedOn.AddMonths(6), LegalDays = vacation.LegalDays, AdditionalDays = vacation.AdditionalDays, Origin = VacationPeriodOrigins.Imported };
                db.VacationPeriods.Add(period);
                var key = $"import:{preview.Sha256}:{row.EmployeeNumber}:{vacation.ReferenceStart:yyyyMMdd}";
                db.VacationMovements.Add(new VacationMovement { VacationPeriod = period, Type = VacationMovementTypes.Credit, LegalDays = vacation.LegalDays, AdditionalDays = vacation.AdditionalDays, OperationKey = key + ":credit", Note = "Saldo histórico importado" });
                if (vacation.UsedLegalDays + vacation.UsedAdditionalDays > 0)
                    db.VacationMovements.Add(new VacationMovement { VacationPeriod = period, Type = VacationMovementTypes.Consume, LegalDays = vacation.UsedLegalDays, AdditionalDays = vacation.UsedAdditionalDays, OperationKey = key + ":used", Note = "Uso histórico importado" });
            }
        }
        var batch = new TenantImportBatch { FileName = preview.FileName, Sha256 = preview.Sha256, AntivirusStatus = preview.AntivirusStatus, Status = "COMPLETED", EmployeeCount = preview.Employees.Count, VacationPeriodCount = preview.Vacations.Count };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return batch.Id;
    }

    private static async Task ParseEmployeesAsync(TenantDbContext db, IXLWorksheet sheet, List<EmployeeImportRow> target, List<ImportValidationError> errors, CancellationToken ct)
    {
        var companies = await db.Companies.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var positions = await db.Positions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var schedules = await db.WorkSchedules.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var existing = await db.Employments.AsNoTracking().Select(x => x.EmployeeNumber).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= last; row++)
        {
            if (sheet.Row(row).Cells(1, 12).All(x => x.IsEmpty())) continue;
            try
            {
                var legajo = Text(sheet, row, 1).ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(legajo) || legajo.Length > 32) throw new InvalidOperationException("Legajo obligatorio o demasiado largo.");
                if (!seen.Add(legajo) || existing.Contains(legajo, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException($"El legajo {legajo} está duplicado.");
                var companyText = Text(sheet, row, 6);
                var company = companies.SingleOrDefault(x => Match(x.Code, x.LegalName, companyText)) ?? throw new InvalidOperationException("La empresa no existe o es ambigua.");
                var departmentText = Text(sheet, row, 7);
                var department = departments.SingleOrDefault(x => x.CompanyId == company.Id && Match(x.Code, x.Name, departmentText)) ?? throw new InvalidOperationException("El departamento no existe en la empresa indicada.");
                var positionText = Text(sheet, row, 8);
                var position = positions.SingleOrDefault(x => x.CompanyId == company.Id && Match(x.Code, x.Name, positionText)) ?? throw new InvalidOperationException("El cargo no existe en la empresa indicada.");
                var scheduleText = Text(sheet, row, 12);
                var schedule = schedules.SingleOrDefault(x => x.Name.Equals(scheduleText, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("La jornada laboral no existe.");
                var document = Text(sheet, row, 2); var names = Text(sheet, row, 3); var surnames = Text(sheet, row, 4);
                if (string.IsNullOrWhiteSpace(document) || string.IsNullOrWhiteSpace(names) || string.IsNullOrWhiteSpace(surnames)) throw new InvalidOperationException("Cédula, nombres y apellidos son obligatorios.");
                var start = sheet.Cell(row, 5).GetDateTime();
                var salary = sheet.Cell(row, 11).GetValue<decimal>();
                if (salary <= 0) throw new InvalidOperationException("El salario bruto debe ser mayor que cero.");
                target.Add(new(legajo, document, names, surnames, DateOnly.FromDateTime(start), company.Id, department.Id, position.Id, Text(sheet, row, 9), Text(sheet, row, 10), salary, schedule.Id));
            }
            catch (Exception ex) { errors.Add(new("Funcionarios", row, ex.Message)); }
        }
    }

    private static void ParseVacations(IXLWorksheet sheet, List<EmployeeImportRow> employees, List<VacationImportRow> target, List<ImportValidationError> errors)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var periods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var row = 2; row <= last; row++)
        {
            if (sheet.Row(row).Cells(1, 8).All(x => x.IsEmpty())) continue;
            try
            {
                var legajo = Text(sheet, row, 1).ToUpperInvariant();
                if (!employees.Any(x => x.EmployeeNumber.Equals(legajo, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("El legajo no pertenece a un funcionario nuevo del archivo.");
                var from = DateOnly.FromDateTime(sheet.Cell(row, 2).GetDateTime());
                var to = DateOnly.FromDateTime(sheet.Cell(row, 3).GetDateTime());
                var accrued = DateOnly.FromDateTime(sheet.Cell(row, 4).GetDateTime());
                var legal = sheet.Cell(row, 5).GetValue<decimal>(); var additional = sheet.Cell(row, 6).GetValue<decimal>();
                var usedLegal = sheet.Cell(row, 7).IsEmpty() ? 0 : sheet.Cell(row, 7).GetValue<decimal>();
                var usedAdditional = sheet.Cell(row, 8).IsEmpty() ? 0 : sheet.Cell(row, 8).GetValue<decimal>();
                if (to < from || accrued <= to) throw new InvalidOperationException("Las fechas del período no son coherentes.");
                if (legal < 0 || additional < 0 || usedLegal < 0 || usedAdditional < 0 || usedLegal > legal || usedAdditional > additional) throw new InvalidOperationException("Los días reconocidos y utilizados no son válidos.");
                if (!periods.Add($"{legajo}:{from:yyyyMMdd}:{to:yyyyMMdd}")) throw new InvalidOperationException("El período está duplicado en el archivo.");
                target.Add(new(legajo, from, to, accrued, legal, additional, usedLegal, usedAdditional));
            }
            catch (Exception ex) { errors.Add(new("Vacaciones", row, ex.Message)); }
        }
    }

    private static string Text(IXLWorksheet sheet, int row, int column) => sheet.Cell(row, column).GetString().Trim();
    private static bool Match(string code, string name, string value) => code.Equals(value, StringComparison.OrdinalIgnoreCase) || name.Equals(value, StringComparison.OrdinalIgnoreCase);
    private static string NormalizeDocument(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static void StyleSheet(IXLWorksheet sheet, int columns)
    {
        var header = sheet.Range(1, 1, 1, columns);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#08243C"); header.Style.Font.FontColor = XLColor.White; header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Columns(1, columns).AdjustToContents();
        for (var column = 1; column <= columns; column++) sheet.Column(column).Width = Math.Min(Math.Max(sheet.Column(column).Width, 14), 28);
        sheet.Range(2, 1, 500, columns).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8D8");
        sheet.ShowGridLines = false;
    }

    private async Task EnsureTenantAdministratorAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (!(await authorizationService.AuthorizeAsync(state.User, AppPolicies.TenantAdministration)).Succeeded)
            throw new UnauthorizedAccessException("No tiene permiso para importar funcionarios.");
    }
}
