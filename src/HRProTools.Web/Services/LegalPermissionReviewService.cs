using System.Security.Cryptography;
using System.Text.Json;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record LegalDocumentView(Guid Id, string Name, string Status, DateTimeOffset UploadedAt);
public sealed record LegalReviewView(string Status, string Comment, string ActorId, DateOnly? DueDate, DateTimeOffset CreatedAt);
public sealed record LegalPermissionDetail(Guid Id, string? Code, string Status, LegalPermissionType? Rule, LegalPermissionInput? Input,
    string ReviewStatus, string? SexAtSubmission, IReadOnlyList<LegalDocumentView> Documents, IReadOnlyList<LegalReviewView> Reviews);

public sealed class LegalPermissionReviewService(TenantDbContextFactory factory, VacationWorkflowService workflow, IFileSecurityScanner scanner)
{
    public async Task<PermissionQuotaSummary> GetSelfQuotaDetailAsync(LegalPermissionInput input, DateOnly start, DateOnly end, string unit, CancellationToken ct = default)
    {
        var self = await workflow.GetSelfLinkAsync(ct);
        await using var db = factory.Create(self.SchemaName);
        var rule = await PermissionRequestService.ResolveRuleAsync(db,self.EmploymentId,input.Code,start,ct);
        return await PermissionQuotaQueries.ReadAsync(db, self.EmploymentId, rule, input, start, end, selectedUnit: unit, ct: ct);
    }

    public async Task<PermissionQuotaSummary?> GetRequestQuotaAsync(Guid id, bool self, CancellationToken ct = default)
    {
        var (schema, user, employment) = await AccessAsync(self, ct);
        await using var db = factory.Create(schema);
        var request = await RequiredAsync(db, id, user, employment, ct);
        if (request.LegalSnapshot is null || request.LegalInput is null) return null;
        var rule = JsonSerializer.Deserialize<LegalPermissionType>(request.LegalSnapshot)!;
        var input = JsonSerializer.Deserialize<LegalPermissionInput>(request.LegalInput)!;
        var scope = self ? null : (await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, user, ct)).ToHashSet();
        return await PermissionQuotaQueries.ReadAsync(db, request.EmploymentId, rule, input, request.StartDate, request.EndDate, selectedUnit: request.Unit, visibleCompanies: scope, historical:true, ct: ct);
    }

    public async Task<string> GetSelfQuotaAsync(string code, DateOnly date, CancellationToken ct = default)
    {
        var summary = await GetSelfQuotaDetailAsync(new() { Code = code, EventDate = date }, date, date, "DAYS", ct);
        return summary.Buckets.Count == 0 ? "Se verifica duración, condiciones y período en cada presentación." :
            string.Join(" ", summary.Buckets.Select(b => $"{b.Period}: {b.Used + b.Reserved:0.###} de {b.Limit:0.###} {LegalDurationInfo.UnitName(b.Unit).ToLowerInvariant()}; utilizados {b.Used:0.###}, reservados {b.Reserved:0.###}, disponibles {b.Available:0.###}."));
    }
    public async Task<LegalPermissionDetail> GetAsync(Guid id, bool self, CancellationToken ct = default)
    {
        var (schema, user, employment) = await AccessAsync(self, ct);
        await using var db = factory.Create(schema);
        var request = await RequiredAsync(db, id, user, employment, ct);
        return new(id, request.LegalCode, request.Status, request.LegalSnapshot is null ? null : JsonSerializer.Deserialize<LegalPermissionType>(request.LegalSnapshot),
            request.LegalInput is null ? null : JsonSerializer.Deserialize<LegalPermissionInput>(request.LegalInput), request.LegalReviewStatus, request.SexAtSubmission,
            await db.PermissionDocuments.AsNoTracking().Where(x => x.PermissionRequestId == id).Select(x => new LegalDocumentView(x.Id, x.OriginalFileName, x.AntivirusStatus, x.UploadedAtUtc)).ToListAsync(ct),
            await db.PermissionLegalReviews.AsNoTracking().Where(x => x.PermissionRequestId == id).OrderByDescending(x => x.CreatedAtUtc).Select(x => new LegalReviewView(x.Status, x.Comment, x.ActorUserId, x.DocumentDueDate, x.CreatedAtUtc)).ToListAsync(ct));
    }

    public async Task UploadAsync(Guid id, bool self, string name, string mime, byte[] content, CancellationToken ct = default)
    {
        var (schema, user, employment) = await AccessAsync(self, ct);
        await using var db = factory.Create(schema);
        await RequiredAsync(db, id, user, employment, ct);
        var valid = AbsenceDocumentValidator.Validate(name, mime, content);
        var scan = await scanner.ScanAsync(content, ct);
        db.PermissionDocuments.Add(new PermissionDocument
        {
            PermissionRequestId = id,
            Content = content,
            Size = content.Length,
            OriginalFileName = Path.GetFileName(name),
            ContentType = valid.ContentType,
            AntivirusStatus = scan.Status,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)),
            UploadedByUserId = user
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReviewAsync(Guid id, string status, string comment, DateOnly? dueDate, CancellationToken ct = default)
    {
        var (schema, user, _) = await AccessAsync(false, ct);
        if (status is not "VERIFIED" and not "NOT_MET" and not "PENDING" || string.IsNullOrWhiteSpace(comment) || comment.Trim().Length is < 20 or > 1000)
            throw new InvalidOperationException("Registre estado y fundamento concreto de 20 a 1000 caracteres, sin diagnósticos innecesarios.");
        await using var db = factory.Create(schema);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE permission_request IN SHARE ROW EXCLUSIVE MODE", ct);
        var request = await RequiredAsync(db, id, user, null, ct);
        if (request.RequestedByUserId == user) throw new UnauthorizedAccessException("No puede verificar su propia solicitud.");
        if (request.Status != PermissionRequestStatuses.Pending || request.LegalSnapshot is null) throw new InvalidOperationException("Solo se revisan solicitudes legales pendientes.");
        if (status == "VERIFIED" && !await db.PermissionDocuments.AnyAsync(x => x.PermissionRequestId == id && x.AntivirusStatus == FileScanStatuses.Clean, ct) && dueDate is null)
            throw new InvalidOperationException("Adjunte documentación limpia o registre expresamente el plazo de entrega posterior aplicable.");
        if (dueDate.HasValue && dueDate < DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Asuncion")))
            throw new InvalidOperationException("El plazo documental no puede estar vencido.");
        request.LegalReviewStatus = status;
        db.PermissionLegalReviews.Add(new PermissionLegalReview { PermissionRequestId = id, Status = status, Comment = comment.Trim(), ActorUserId = user, DocumentDueDate = dueDate });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RegisterNoticeAsync(Guid employmentId, DateOnly start, DateOnly end, string mode, string reference, CancellationToken ct = default)
    {
        var (schema, user, _) = await AccessAsync(false, ct);
        await using var db = factory.Create(schema);
        var companies = await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, user, ct);
        if (!await db.Employments.AnyAsync(x => x.Id == employmentId && companies.Contains(x.CompanyId), ct)) throw new UnauthorizedAccessException("Vínculo fuera de su alcance.");
        if (end < start || mode is not "DAYS" and not "HOURS" || reference.Trim().Length is < 10 or > 500) throw new InvalidOperationException("Indique período, modalidad exclusiva y referencia del preaviso de despido.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE employment_notice IN SHARE ROW EXCLUSIVE MODE", ct);
        if (await db.EmploymentNotices.AnyAsync(x => x.EmploymentId == employmentId && x.StartDate <= end && x.EndDate >= start, ct)) throw new InvalidOperationException("Ya existe preaviso verificado para ese período.");
        db.EmploymentNotices.Add(new EmploymentNotice { EmploymentId = employmentId, StartDate = start, EndDate = end, Mode = mode, Reference = reference.Trim(), VerifiedByUserId = user });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<(string Schema, string User, Guid? Employment)> AccessAsync(bool self, CancellationToken ct)
    {
        if (self) { var link = await workflow.GetSelfLinkAsync(ct); return (link.SchemaName, link.UserId, link.EmploymentId); }
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        if (!actor.IsHumanResources) throw new UnauthorizedAccessException("La documentación y revisión legal están reservadas a Recursos Humanos.");
        return (actor.SchemaName, actor.UserId, null);
    }
    private static async Task<PermissionRequest> RequiredAsync(TenantDbContext db, Guid id, string user, Guid? employment, CancellationToken ct)
    {
        var query = db.PermissionRequests.Where(x => x.Id == id);
        if (employment.HasValue) query = query.Where(x => x.EmploymentId == employment && x.RequestedByUserId == user);
        else { var companies = await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, user, ct); query = query.Where(x => companies.Contains(x.CompanyId)); }
        return await query.SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException("Solicitud no disponible dentro de su alcance.");
    }
}
