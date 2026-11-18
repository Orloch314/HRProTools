using HRProTools.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public static class TenantLogoEndpointExtensions
{
    public static IEndpointConventionBuilder MapTenantLogo(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/tenant/logo", async (
            HttpContext httpContext,
            UserManager<ApplicationUser> users,
            ApplicationDbContext globalDb,
            TenantDbContextFactory contextFactory,
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var user = await users.GetUserAsync(httpContext.User);
            if (user is null || !user.IsActive || user.TenantId is null)
                return Results.NotFound();

            var schemaName = await globalDb.Tenants.AsNoTracking()
                .Where(x => x.Id == user.TenantId && x.Status == TenantStatuses.Active)
                .Select(x => x.SchemaName)
                .SingleOrDefaultAsync(cancellationToken);
            if (schemaName is null)
                return Results.NotFound();

            await using var tenantDb = contextFactory.Create(schemaName);
            var logo = await tenantDb.TenantBranding.AsNoTracking()
                .Where(x => x.Id == TenantBranding.SingletonId)
                .Select(x => new { x.LogoData, x.LogoContentType, x.AntivirusStatus })
                .SingleOrDefaultAsync(cancellationToken);
            if (logo is null ||
                !TenantLogoFileValidator.IsAllowedContentType(logo.LogoContentType) ||
                (logo.AntivirusStatus != FileScanStatuses.Clean &&
                 !(environment.IsDevelopment() && logo.AntivirusStatus == FileScanStatuses.DevelopmentUnscanned)))
                return Results.NotFound();

            httpContext.Response.Headers.CacheControl = "private, no-cache";
            return Results.File(logo.LogoData, logo.LogoContentType);
        }).RequireAuthorization();
    }
}
