using System.Security.Claims;
using HRProTools.Web.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class CurrentTenantService(
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopeFactory)
{
    public async Task<Tenant> GetRequiredAsync(CancellationToken cancellationToken = default)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userId = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("La sesion no corresponde a un usuario valido.");

        // Blazor can initialize the layout and the page concurrently. A dedicated
        // scope prevents both component trees from sharing Identity's scoped DbContext.
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await (
                from user in globalDb.Users.AsNoTracking()
                join tenant in globalDb.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
                where user.Id == userId && user.IsActive && tenant.Status == TenantStatuses.Active
                select tenant)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("El usuario no tiene un grupo empresarial activo.");
    }
}
