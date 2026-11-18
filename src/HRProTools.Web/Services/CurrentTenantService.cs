using HRProTools.Web.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class CurrentTenantService(
    AuthenticationStateProvider authenticationStateProvider,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext globalDb)
{
    public async Task<Tenant> GetRequiredAsync(CancellationToken cancellationToken = default)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(state.User)
            ?? throw new UnauthorizedAccessException("La sesion no corresponde a un usuario valido.");
        if (!user.IsActive || user.TenantId is null)
            throw new UnauthorizedAccessException("El usuario no tiene un grupo empresarial activo.");

        return await globalDb.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == user.TenantId && x.Status == TenantStatuses.Active, cancellationToken)
            ?? throw new UnauthorizedAccessException("El grupo empresarial no esta activo.");
    }
}
