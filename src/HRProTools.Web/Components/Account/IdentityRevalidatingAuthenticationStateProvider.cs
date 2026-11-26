using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Components.Account;

// This is a server-side AuthenticationStateProvider that revalidates the security stamp for the connected user
// every 30 minutes an interactive circuit is connected.
internal sealed class IdentityRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // Get the user manager from a new scope to ensure it fetches fresh data
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await ValidateSecurityStampAsync(userManager, db, authenticationState.User, cancellationToken);
    }

    private async Task<bool> ValidateSecurityStampAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext db, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return false;
        }
        if (!user.IsActive || user.MustChangePassword) return false;
        if (user.TenantId is Guid tenantId &&
            !await db.Tenants.AsNoTracking().AnyAsync(x => x.Id == tenantId && x.Status == TenantStatuses.Active, cancellationToken))
            return false;
        else if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }
        else
        {
            var principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
            var userStamp = await userManager.GetSecurityStampAsync(user);
            return principalStamp == userStamp;
        }
    }
}
