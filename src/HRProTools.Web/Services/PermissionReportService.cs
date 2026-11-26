using ClosedXML.Excel;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record PermissionReportFilter(Guid? CompanyId, Guid? DepartmentId, DateOnly? From, DateOnly? To, string? Status);
public sealed record PermissionReportOption(Guid Id, string Name, Guid? CompanyId = null);
public sealed record PermissionReportOptions(IReadOnlyList<PermissionReportOption> Companies, IReadOnlyList<PermissionReportOption> Departments);

public sealed class PermissionReportService(TenantDbContextFactory tenantFactory, VacationWorkflowService workflow)
{
    public async Task<PermissionReportOptions> GetOptionsAsync(CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        if (!actor.IsHumanResources) throw new UnauthorizedAccessException("El reporte esta disponible solamente para Recursos Humanos.");
        await using var db = tenantFactory.Create(actor.SchemaName);
        var companies = await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, actor.UserId, ct);
        return new(
            await db.Companies.AsNoTracking().Where(x => companies.Contains(x.Id) && x.IsActive).OrderBy(x => x.LegalName).Select(x => new PermissionReportOption(x.Id, x.LegalName, null)).ToListAsync(ct),
            await db.Departments.AsNoTracking().Where(x => companies.Contains(x.CompanyId) && x.IsActive).OrderBy(x => x.Name).Select(x => new PermissionReportOption(x.Id, x.Name, x.CompanyId)).ToListAsync(ct));
    }

    public static async Task<byte[]> GenerateAsync(TenantDbContext db, string userId, PermissionReportFilter filter, CancellationToken ct = default)
    {
        var companies = await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, userId, ct);
        if (filter.CompanyId.HasValue && !companies.Contains(filter.CompanyId.Value))
            throw new UnauthorizedAccessException("La empresa seleccionada esta fuera de su alcance.");
        if (filter.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == filter.DepartmentId && companies.Contains(x.CompanyId), ct))
            throw new UnauthorizedAccessException("El departamento seleccionado esta fuera de su alcance.");
        if (filter.From.HasValue && filter.To.HasValue && filter.To < filter.From)
            throw new InvalidOperationException("La fecha final no puede ser anterior a la fecha inicial.");
        var statuses = new[] { PermissionRequestStatuses.Approved, PermissionRequestStatuses.Rejected, PermissionRequestStatuses.Pending, PermissionRequestStatuses.Cancelled };
        if (!string.IsNullOrWhiteSpace(filter.Status) && !statuses.Contains(filter.Status))
            throw new InvalidOperationException("El estado seleccionado no esta admitido.");

        var query = db.PermissionRequests.AsNoTracking().Where(x => companies.Contains(x.CompanyId));
        if (filter.CompanyId.HasValue) query = query.Where(x => x.CompanyId == filter.CompanyId);
        if (filter.DepartmentId.HasValue) query = query.Where(x => x.DepartmentId == filter.DepartmentId);
        if (filter.From.HasValue) query = query.Where(x => x.StartDate >= filter.From);
        if (filter.To.HasValue) query = query.Where(x => x.StartDate <= filter.To);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        var rows = await query.Select(x => new
        {
            Company = x.Employment.Company.LegalName,
            Department = db.Departments.Where(d => d.Id == x.DepartmentId).Select(d => d.Name).Single(),
            x.Employment.EmployeeNumber,
            Name = x.Employment.Person.GivenNames + " " + x.Employment.Person.FamilyNames,
            x.PermissionType,
            x.LegalCode,
            x.LegalSnapshot,
            x.Unit,
            x.StartDate,
            x.EndDate,
            x.RequestedDays,
            x.RequestedMinutes,
            x.CreatedAtUtc,
            x.Status,
            x.DecidedAtUtc
        }).OrderBy(x => x.Company).ThenBy(x => x.Department).ThenBy(x => x.StartDate).ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Permisos");
        sheet.ShowGridLines = false;
        sheet.Cell("A1").Value = "Reporte de permisos";
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 16;
        sheet.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#08243C");
        sheet.Cell("A2").Value = "Filtros aplicados";
        sheet.Cell("B2").Value = FilterDescription(filter);
        var headers = new[] { "Empresa", "Departamento", "Legajo", "Nombre y apellido", "Tipo de permiso", "Goce", "Unidad", "Fecha inicial", "Fecha final", "Cantidad", "Fecha de solicitud", "Estado", "Fecha de resolucion final" };
        for (var column = 1; column <= headers.Length; column++) sheet.Cell(4, column).Value = headers[column - 1];
        var rowNumber = 5;
        foreach (var row in rows)
        {
            sheet.Cell(rowNumber, 1).Value = row.Company;
            sheet.Cell(rowNumber, 2).Value = row.Department;
            sheet.Cell(rowNumber, 3).Value = row.EmployeeNumber;
            sheet.Cell(rowNumber, 4).Value = row.Name;
            sheet.Cell(rowNumber, 5).Value = PermissionRequestService.SnapshotName(row.LegalSnapshot) ?? row.LegalCode ?? "Permiso";
            sheet.Cell(rowNumber, 6).Value = row.LegalCode is "MATERNIDAD" or "ADOPCION" ? "Régimen legal, sin cálculo IPS" : row.PermissionType == PermissionRequestTypes.Paid ? "Con goce" : "Sin goce";
            sheet.Cell(rowNumber, 7).Value = row.Unit == PermissionRequestUnits.Days ? "Dias" : "Horas";
            sheet.Cell(rowNumber, 8).Value = row.StartDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(rowNumber, 9).Value = row.EndDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(rowNumber, 10).Value = row.Unit == PermissionRequestUnits.Days ? row.RequestedDays : row.RequestedMinutes / 60m;
            sheet.Cell(rowNumber, 11).Value = row.CreatedAtUtc.UtcDateTime;
            sheet.Cell(rowNumber, 12).Value = StatusText(row.Status);
            if (row.DecidedAtUtc.HasValue) sheet.Cell(rowNumber, 13).Value = row.DecidedAtUtc.Value.UtcDateTime;
            rowNumber++;
        }
        var lastRow = Math.Max(5, rowNumber - 1);
        var table = sheet.Range(4, 1, lastRow, headers.Length).CreateTable("PermisosTable");
        table.Theme = XLTableTheme.TableStyleMedium2;
        table.ShowAutoFilter = true;
        sheet.SheetView.FreezeRows(4);
        sheet.Range(5, 8, lastRow, 9).Style.DateFormat.Format = "dd/mm/yyyy";
        sheet.Range(5, 11, lastRow, 11).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Range(5, 13, lastRow, 13).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Range(5, 10, lastRow, 10).Style.NumberFormat.Format = "0.00";
        sheet.Columns().AdjustToContents();
        foreach (var width in new Dictionary<int, double> { [1] = 28, [2] = 24, [3] = 14, [4] = 30, [5] = 22, [6] = 14, [7] = 12, [12] = 16 })
            sheet.Column(width.Key).Width = Math.Min(sheet.Column(width.Key).Width, width.Value);
        sheet.Range(1, 1, lastRow, headers.Length).Style.Font.FontName = "Arial";
        sheet.Row(4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Row(4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string FilterDescription(PermissionReportFilter filter) =>
        $"Empresa: {(filter.CompanyId.HasValue ? "seleccionada" : "todas autorizadas")}; Departamento: {(filter.DepartmentId.HasValue ? "seleccionado" : "todos autorizados")}; Desde: {(filter.From.HasValue ? filter.From.Value.ToString("dd/MM/yyyy") : "sin limite")}; Hasta: {(filter.To.HasValue ? filter.To.Value.ToString("dd/MM/yyyy") : "sin limite")}; Estado: {(string.IsNullOrWhiteSpace(filter.Status) ? "todos" : StatusText(filter.Status!))}";

    private static string StatusText(string status) => status switch
    {
        PermissionRequestStatuses.Approved => "Aprobado",
        PermissionRequestStatuses.Rejected => "Rechazado",
        PermissionRequestStatuses.Pending => "Pendiente",
        PermissionRequestStatuses.Cancelled => "Cancelado",
        _ => status
    };
}
