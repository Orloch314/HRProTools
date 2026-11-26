using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public static class Phase5EndpointExtensions
{
    public static IEndpointRouteBuilder MapPhase5Endpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/tenant/reportes/permisos.xlsx", async (
            HttpContext http, ApplicationDbContext global, TenantDbContextFactory factory,
            Guid? companyId, Guid? departmentId, DateOnly? from, DateOnly? to, string? status, CancellationToken ct) =>
        {
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
            var tenant = await (from user in global.Users.AsNoTracking()
                                join item in global.Tenants.AsNoTracking() on user.TenantId equals item.Id
                                where user.Id == userId && user.IsActive && item.Status == TenantStatuses.Active
                                select item).SingleAsync(ct);
            await using var db = factory.Create(tenant.SchemaName);
            var bytes = await PermissionReportService.GenerateAsync(db, userId, new(companyId, departmentId, from, to, status), ct);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Permisos-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
        }).RequireAuthorization(AppPolicies.HumanResourcesOperations);

        endpoints.MapGet("/tenant/ausencias/documentos/{documentId:guid}", async (
            Guid documentId, HttpContext http, ApplicationDbContext global, TenantDbContextFactory factory, CancellationToken ct) =>
        {
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
            var tenant = await (from user in global.Users.AsNoTracking()
                                join item in global.Tenants.AsNoTracking() on user.TenantId equals item.Id
                                where user.Id == userId && user.IsActive && item.Status == TenantStatuses.Active
                                select item).SingleAsync(ct);
            await using var db = factory.Create(tenant.SchemaName);
            var companies = await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, userId, ct);
            var document = await db.AbsenceDocuments.AsNoTracking().Where(x => x.Id == documentId && companies.Contains(x.Absence.CompanyId))
                .Select(x => new { x.Content, x.ContentType, x.OriginalFileName, x.AntivirusStatus }).SingleOrDefaultAsync(ct);
            if (document is null) return Results.NotFound();
            if (document.AntivirusStatus != FileScanStatuses.Clean) return Results.Problem("El documento no fue validado como seguro.", statusCode: StatusCodes.Status423Locked);
            return Results.File(document.Content, document.ContentType, document.OriginalFileName, enableRangeProcessing: false);
        }).RequireAuthorization(AppPolicies.HumanResourcesOperations);
        endpoints.MapGet("/tenant/permisos/documentos/{documentId:guid}", async (
            Guid documentId, HttpContext http, ApplicationDbContext global, TenantDbContextFactory factory, CancellationToken ct) =>
        {
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
            var tenant = await (from user in global.Users.AsNoTracking()
                                join item in global.Tenants.AsNoTracking() on user.TenantId equals item.Id
                                where user.Id == userId && user.IsActive && item.Status == TenantStatuses.Active
                                select item).SingleAsync(ct);
            await using var db = factory.Create(tenant.SchemaName);
            var companies = await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, userId, ct);
            var document = await db.PermissionDocuments.AsNoTracking().Where(x => x.Id == documentId && companies.Contains(x.PermissionRequest.CompanyId))
                .Select(x => new { x.Content, x.ContentType, x.OriginalFileName, x.AntivirusStatus }).SingleOrDefaultAsync(ct);
            if (document is null) return Results.NotFound();
            if (document.AntivirusStatus != FileScanStatuses.Clean) return Results.Problem("El documento no fue validado como seguro.", statusCode: StatusCodes.Status423Locked);
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(document.Content, document.ContentType, document.OriginalFileName);
        }).RequireAuthorization(AppPolicies.HumanResourcesOperations);
        return endpoints;
    }
}
