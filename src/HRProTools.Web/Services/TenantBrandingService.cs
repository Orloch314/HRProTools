using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record TenantLogoInfo(
    string FileName,
    string ContentType,
    int Size,
    string AntivirusStatus,
    DateTimeOffset UpdatedAtUtc);

public sealed record SaveTenantLogoResult(TenantLogoInfo Logo, bool WasScanned);

public sealed class TenantBrandingService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory contextFactory,
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorizationService,
    IFileSecurityScanner fileScanner,
    IWebHostEnvironment environment)
{
    public event Action? Changed;

    public async Task<TenantLogoInfo?> GetLogoInfoAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var db = contextFactory.Create(tenant.SchemaName);
        var item = await db.TenantBranding.AsNoTracking()
            .Where(x => x.Id == TenantBranding.SingletonId)
            .Select(x => new TenantLogoInfo(
                x.LogoFileName,
                x.LogoContentType,
                x.LogoData.Length,
                x.AntivirusStatus,
                x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return item is not null && IsUsable(item.AntivirusStatus) ? item : null;
    }

    public async Task<SaveTenantLogoResult> SaveLogoAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        await EnsureTenantAdministratorAsync();
        var validated = TenantLogoFileValidator.Validate(content);
        var scan = await fileScanner.ScanAsync(content, cancellationToken);
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);

        await using var db = contextFactory.Create(tenant.SchemaName);
        var item = await db.TenantBranding
            .SingleOrDefaultAsync(x => x.Id == TenantBranding.SingletonId, cancellationToken)
            ?? new TenantBranding();
        if (db.Entry(item).State == EntityState.Detached)
            db.TenantBranding.Add(item);

        item.LogoData = content;
        item.LogoContentType = validated.ContentType;
        item.LogoFileName = NormalizeFileName(fileName);
        item.AntivirusStatus = scan.Status;
        item.ScannedAtUtc = scan.ScannedAtUtc;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var info = new TenantLogoInfo(
            item.LogoFileName,
            item.LogoContentType,
            item.LogoData.Length,
            item.AntivirusStatus,
            item.UpdatedAtUtc);
        Changed?.Invoke();
        return new SaveTenantLogoResult(info, scan.WasScanned);
    }

    public async Task DeleteLogoAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTenantAdministratorAsync();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var db = contextFactory.Create(tenant.SchemaName);
        var item = await db.TenantBranding
            .SingleOrDefaultAsync(x => x.Id == TenantBranding.SingletonId, cancellationToken);
        if (item is not null)
        {
            db.TenantBranding.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }
        Changed?.Invoke();
    }

    private bool IsUsable(string status) =>
        status == FileScanStatuses.Clean ||
        (environment.IsDevelopment() && status == FileScanStatuses.DevelopmentUnscanned);

    private async Task EnsureTenantAdministratorAsync()
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var authorization = await authorizationService.AuthorizeAsync(
            authenticationState.User,
            AppPolicies.TenantAdministration);
        if (!authorization.Succeeded)
            throw new UnauthorizedAccessException("No tiene permiso para modificar la identidad visual.");
    }

    private static string NormalizeFileName(string value)
    {
        var name = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(name))
            name = "logo";
        return name.Length <= 255 ? name : name[..255];
    }
}
