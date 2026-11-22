using System.Security.Claims;
using System.Security.Cryptography;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record AbsenceEmployeeOption(Guid Id, string EmployeeNumber, string Name, string Company, string Department);
public sealed record AbsenceConflict(string Kind, string Message);
public sealed record AbsenceHistoryView(string PreviousStatus, string NewStatus, string? PreviousClassification, string? NewClassification, string ChangedBy, string Comment, DateTimeOffset ChangedAtUtc);
public sealed record AbsenceDocumentView(Guid Id, string FileName, string ContentType, long Size, string Sha256, string AntivirusStatus, DateTimeOffset UploadedAtUtc, bool CanDownload);
public sealed record AbsenceView(
    Guid Id, Guid EmploymentId, string EmployeeNumber, string EmployeeName, string Company, string Department,
    DateOnly StartDate, DateOnly EndDate, int AffectedWorkdays, string DeclaredType, string? FinalClassification,
    string Status, string Reason, string RegisteredByRole, DateTimeOffset CreatedAtUtc, string ResolutionComment,
    bool CanReview, IReadOnlyList<AbsenceHistoryView> History, IReadOnlyList<AbsenceDocumentView> Documents);

public sealed class AbsenceService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory tenantFactory,
    VacationWorkflowService workflow,
    IFileSecurityScanner scanner,
    IWebHostEnvironment environment)
{
    public async Task<IReadOnlyList<AbsenceConflict>> PreviewSelfAsync(Guid employmentId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var self = await workflow.GetSelfLinkAsync(ct);
        if (self.EmploymentId != employmentId) throw new UnauthorizedAccessException();
        await using var db = tenantFactory.Create(self.SchemaName);
        _ = await WorkCalendarService.CalculatePermissionDaysAsync(db, employmentId, start, end, ct);
        return await FindConflictsAsync(db, employmentId, start, end, ct);
    }

    public async Task<Guid> CreateSelfAsync(DateOnly start, DateOnly end, string declaredType, string reason, CancellationToken ct = default)
    {
        var self = await workflow.GetSelfLinkAsync(ct);
        return await CreateAsync(self.SchemaName, self.UserId, AppRoles.Employee, self.EmploymentId, start, end, declaredType, reason, false, ct);
    }

    public async Task<IReadOnlyList<AbsenceEmployeeOption>> GetOperationalEmployeesAsync(CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        await using var db = tenantFactory.Create(actor.SchemaName);
        var scope = await ResolveScopeAsync(db, actor, ct);
        return await db.Employments.AsNoTracking()
            .Where(x => x.Status == EmploymentStatuses.Active &&
                ((actor.IsHumanResources && scope.CompanyIds.Contains(x.CompanyId)) ||
                 (actor.IsApprover && db.EmploymentAssignments.Any(a => a.EmploymentId == x.Id && a.ValidTo == null && scope.DepartmentIds.Contains(a.DepartmentId)))))
            .Select(x => new AbsenceEmployeeOption(
                x.Id, x.EmployeeNumber, x.Person.GivenNames + " " + x.Person.FamilyNames, x.Company.LegalName,
                db.EmploymentAssignments.Where(a => a.EmploymentId == x.Id && a.ValidTo == null).Select(a => a.Department.Name).Single()))
            .OrderBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<Guid> CreateOperationalAsync(Guid employmentId, DateOnly start, DateOnly end, string declaredType, string reason, bool confirmConflict, CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        await using (var db = tenantFactory.Create(actor.SchemaName))
        {
            await EnsureEmploymentScopeAsync(db, actor, employmentId, false, ct);
        }
        var role = actor.IsHumanResources ? AppRoles.HumanResources : AppRoles.Approver;
        return await CreateAsync(actor.SchemaName, actor.UserId, role, employmentId, start, end, declaredType, reason, confirmConflict, ct);
    }

    public async Task<List<AbsenceView>> GetSelfAsync(CancellationToken ct = default)
    {
        var self = await workflow.GetSelfLinkAsync(ct);
        await using var db = tenantFactory.Create(self.SchemaName);
        return await QueryViewsAsync(db, db.Absences.Where(x => x.EmploymentId == self.EmploymentId), false, false, ct);
    }

    public async Task<List<AbsenceView>> GetOperationalAsync(CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        await using var db = tenantFactory.Create(actor.SchemaName);
        var scope = await ResolveScopeAsync(db, actor, ct);
        var query = db.Absences.Where(x =>
            (actor.IsHumanResources && scope.CompanyIds.Contains(x.CompanyId)) ||
            (actor.IsApprover && scope.DepartmentIds.Contains(x.DepartmentId)));
        return await QueryViewsAsync(db, query, actor.IsHumanResources, actor.IsHumanResources, ct);
    }

    public async Task ReviewAsync(Guid absenceId, string finalClassification, string finalStatus, string comment, CancellationToken ct = default)
    {
        ValidateType(finalClassification);
        if (finalStatus is not AbsenceStatuses.Justified and not AbsenceStatuses.Unjustified)
            throw new InvalidOperationException("La resolucion final debe ser justificada o injustificada.");
        if ((finalStatus == AbsenceStatuses.Unjustified) != (finalClassification == AbsenceTypes.Unjustified))
            throw new InvalidOperationException("La clasificacion y el estado final son contradictorios.");
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        if (!actor.IsHumanResources) throw new UnauthorizedAccessException("Solo Recursos Humanos puede resolver una ausencia.");
        await using var db = tenantFactory.Create(actor.SchemaName);
        var item = await db.Absences.SingleOrDefaultAsync(x => x.Id == absenceId, ct) ?? throw new InvalidOperationException("La ausencia no existe.");
        await EnsureEmploymentScopeAsync(db, actor, item.EmploymentId, true, ct);
        if (item.Status == AbsenceStatuses.Cancelled) throw new InvalidOperationException("La ausencia esta cancelada.");
        var previousStatus = item.Status;
        var previousClassification = item.FinalClassification;
        item.FinalClassification = finalClassification;
        item.Status = finalStatus;
        item.ResolutionComment = comment.Trim();
        item.ResolvedByUserId = actor.UserId;
        item.ResolvedAtUtc = DateTimeOffset.UtcNow;
        db.AbsenceHistory.Add(new AbsenceHistory
        {
            AbsenceId = item.Id,
            PreviousStatus = previousStatus,
            NewStatus = item.Status,
            PreviousClassification = previousClassification,
            NewClassification = item.FinalClassification,
            ChangedByUserId = actor.UserId,
            Comment = item.ResolutionComment
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<AbsenceDocumentView> UploadDocumentAsync(Guid absenceId, string fileName, string declaredContentType, byte[] content, CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        if (!actor.IsHumanResources) throw new UnauthorizedAccessException("Solo Recursos Humanos puede cargar documentos justificativos.");
        var validated = AbsenceDocumentValidator.Validate(fileName, declaredContentType, content);
        var scan = await scanner.ScanAsync(content, ct);
        await using var db = tenantFactory.Create(actor.SchemaName);
        var absence = await db.Absences.AsNoTracking().SingleOrDefaultAsync(x => x.Id == absenceId, ct) ?? throw new InvalidOperationException("La ausencia no existe.");
        await EnsureEmploymentScopeAsync(db, actor, absence.EmploymentId, true, ct);
        var document = new AbsenceDocument
        {
            AbsenceId = absenceId,
            OriginalFileName = NormalizeFileName(fileName),
            ContentType = validated.ContentType,
            Size = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            Content = content,
            AntivirusStatus = scan.Status,
            ScannedAtUtc = scan.ScannedAtUtc,
            UploadedByUserId = actor.UserId
        };
        db.AbsenceDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return MapDocument(document);
    }

    public async Task MarkCancelledAsync(Guid absenceId, string reason, CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        if (!actor.IsHumanResources) throw new UnauthorizedAccessException("Solo Recursos Humanos puede cancelar una ausencia registrada.");
        await using var db = tenantFactory.Create(actor.SchemaName);
        var item = await db.Absences.SingleOrDefaultAsync(x => x.Id == absenceId, ct) ?? throw new InvalidOperationException("La ausencia no existe.");
        await EnsureEmploymentScopeAsync(db, actor, item.EmploymentId, true, ct);
        if (item.Status == AbsenceStatuses.Cancelled) return;
        var previous = item.Status;
        item.Status = AbsenceStatuses.Cancelled;
        item.CancelledByUserId = actor.UserId;
        item.CancelledAtUtc = DateTimeOffset.UtcNow;
        item.CancellationReason = reason.Trim();
        db.AbsenceHistory.Add(new AbsenceHistory { AbsenceId = item.Id, PreviousStatus = previous, NewStatus = item.Status, PreviousClassification = item.FinalClassification, NewClassification = item.FinalClassification, ChangedByUserId = actor.UserId, Comment = item.CancellationReason });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Guid> CreateAsync(string schema, string userId, string role, Guid employmentId, DateOnly start, DateOnly end, string type, string reason, bool confirmConflict, CancellationToken ct)
    {
        ValidateType(type);
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Indique un motivo o comentario.");
        await using var db = tenantFactory.Create(schema);
        var calendar = await WorkCalendarService.CalculatePermissionDaysAsync(db, employmentId, start, end, ct);
        var assignment = await db.EmploymentAssignments.AsNoTracking().Where(x => x.EmploymentId == employmentId && x.ValidTo == null)
            .Select(x => new { x.DepartmentId, x.Department.CompanyId }).SingleAsync(ct);
        var conflicts = await FindConflictsAsync(db, employmentId, start, end, ct);
        if (conflicts.Any(x => x.Kind == "ABSENCE")) throw new InvalidOperationException("Ya existe una ausencia activa para parte del intervalo.");
        if (conflicts.Count != 0 && !confirmConflict)
            throw new InvalidOperationException(string.Join(" ", conflicts.Select(x => x.Message)) + " Un usuario autorizado debe confirmar expresamente el registro sin modificar la solicitud existente.");
        var item = new Absence
        {
            EmploymentId = employmentId,
            CompanyId = assignment.CompanyId,
            DepartmentId = assignment.DepartmentId,
            StartDate = start,
            EndDate = end,
            AffectedWorkdays = calendar.RequestedDays,
            DeclaredType = type,
            Status = role == AppRoles.Employee ? AbsenceStatuses.Declared : AbsenceStatuses.PendingReview,
            Reason = reason.Trim(),
            RegisteredByUserId = userId,
            RegisteredByRole = role,
            Dates = calendar.Dates.Select(x => new AbsenceDay { Date = x.Date, WorkScheduleId = x.WorkScheduleId, WorkScheduleName = x.WorkScheduleName, IsScheduledWorkday = x.IsScheduledWorkday, IsHoliday = x.IsHoliday, HolidayName = x.HolidayName, CountsAsAbsenceDay = x.CountsAsPermissionDay }).ToList()
        };
        item.History.Add(new AbsenceHistory { PreviousStatus = string.Empty, NewStatus = item.Status, NewClassification = null, ChangedByUserId = userId, Comment = "Registro inicial" });
        db.Absences.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    private static async Task<List<AbsenceConflict>> FindConflictsAsync(TenantDbContext db, Guid employmentId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var result = new List<AbsenceConflict>();
        if (await db.Absences.AnyAsync(x => x.EmploymentId == employmentId && x.Status != AbsenceStatuses.Cancelled && x.StartDate <= end && x.EndDate >= start, ct))
            result.Add(new("ABSENCE", "Existe una ausencia activa superpuesta."));
        if (await db.VacationRequests.AnyAsync(x => x.EmploymentId == employmentId && (x.Status == VacationRequestStatuses.Pending || x.Status == VacationRequestStatuses.Approved) && x.StartDate <= end && x.EndDate >= start, ct))
            result.Add(new("VACATION", "Existe una solicitud de vacaciones pendiente o aprobada superpuesta."));
        if (await db.PermissionRequests.AnyAsync(x => x.EmploymentId == employmentId && (x.Status == PermissionRequestStatuses.Pending || x.Status == PermissionRequestStatuses.Approved) && x.StartDate <= end && x.EndDate >= start, ct))
            result.Add(new("PERMISSION", "Existe una solicitud de permiso pendiente o aprobada superpuesta."));
        return result;
    }

    private async Task<List<AbsenceView>> QueryViewsAsync(TenantDbContext db, IQueryable<Absence> query, bool canReview, bool includeDocuments, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().Include(x => x.Employment).ThenInclude(x => x.Person).Include(x => x.Company).Include(x => x.Department)
            .Include(x => x.History).Include(x => x.Documents).OrderByDescending(x => x.StartDate).ToListAsync(ct);
        var userIds = rows.SelectMany(x => x.History.Select(h => h.ChangedByUserId)).Distinct().ToList();
        var tenant = await currentTenant.GetRequiredAsync(ct);
        var names = await workflow.GetUserNamesAsync(tenant.Id, userIds, ct);
        return rows.Select(x => new AbsenceView(x.Id, x.EmploymentId, x.Employment.EmployeeNumber,
            x.Employment.Person.GivenNames + " " + x.Employment.Person.FamilyNames, x.Company.LegalName, x.Department.Name,
            x.StartDate, x.EndDate, x.AffectedWorkdays, x.DeclaredType, x.FinalClassification, x.Status, x.Reason,
            x.RegisteredByRole, x.CreatedAtUtc, x.ResolutionComment, canReview,
            x.History.OrderByDescending(h => h.ChangedAtUtc).Select(h => new AbsenceHistoryView(h.PreviousStatus, h.NewStatus, h.PreviousClassification, h.NewClassification, names.GetValueOrDefault(h.ChangedByUserId, "Usuario"), h.Comment, h.ChangedAtUtc)).ToList(),
            includeDocuments ? x.Documents.OrderByDescending(d => d.UploadedAtUtc).Select(MapDocument).ToList() : [])).ToList();
    }

    private async Task EnsureEmploymentScopeAsync(TenantDbContext db, RequestWorkflowActor actor, Guid employmentId, bool requireHr, CancellationToken ct)
    {
        if (requireHr && !actor.IsHumanResources) throw new UnauthorizedAccessException();
        var assignment = await db.EmploymentAssignments.AsNoTracking().Where(x => x.EmploymentId == employmentId && x.ValidTo == null)
            .Select(x => new { x.DepartmentId, x.Department.CompanyId }).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("El funcionario no tiene asignacion vigente.");
        var scope = await ResolveScopeAsync(db, actor, ct);
        if (!((actor.IsHumanResources && scope.CompanyIds.Contains(assignment.CompanyId)) || (!requireHr && actor.IsApprover && scope.DepartmentIds.Contains(assignment.DepartmentId))))
            throw new UnauthorizedAccessException("El funcionario esta fuera de su alcance autorizado.");
    }

    private static async Task<(HashSet<Guid> CompanyIds, HashSet<Guid> DepartmentIds)> ResolveScopeAsync(TenantDbContext db, RequestWorkflowActor actor, CancellationToken ct)
    {
        var companies = new HashSet<Guid>();
        if (actor.IsHumanResources)
        {
            var scopes = await db.HumanResourcesScopes.AsNoTracking().Where(x => x.UserId == actor.UserId).Select(x => x.CompanyId).ToListAsync(ct);
            companies = scopes.Any(x => x is null)
                ? (await db.Companies.AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet()
                : scopes.Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        }
        var departments = actor.IsApprover
            ? (await db.DepartmentApproverAssignments.AsNoTracking().Where(x => x.UserId == actor.UserId).Select(x => x.DepartmentId).ToListAsync(ct)).ToHashSet()
            : [];
        return (companies, departments);
    }

    private AbsenceDocumentView MapDocument(AbsenceDocument x) => new(x.Id, x.OriginalFileName, x.ContentType, x.Size, x.Sha256, x.AntivirusStatus, x.UploadedAtUtc,
        x.AntivirusStatus == FileScanStatuses.Clean && (environment.IsDevelopment() || environment.IsProduction()));

    private static void ValidateType(string value)
    {
        if (value is not AbsenceTypes.Medical and not AbsenceTypes.Unjustified and not AbsenceTypes.OtherJustified)
            throw new InvalidOperationException("El tipo de ausencia no esta admitido.");
    }

    private static string NormalizeFileName(string value)
    {
        var name = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(name)) name = "documento";
        return name.Length <= 255 ? name : name[..255];
    }
}
