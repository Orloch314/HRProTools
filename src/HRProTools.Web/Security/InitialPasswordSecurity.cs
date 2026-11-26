using System.Security.Claims;
using HRProTools.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;

namespace HRProTools.Web.Security;

public sealed class InitialPasswordClaimsFactory(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(users, roles, options)
{
    public const string Claim = "hrprotools:password-change-required";
    public static bool Ready(AuthorizationHandlerContext context) => !context.User.HasClaim(Claim, "true");
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.MustChangePassword) identity.AddClaim(new Claim(Claim, "true"));
        return identity;
    }
}

public sealed class InitialPasswordMiddleware(RequestDelegate next)
{
    public const string ChangePath = "/Account/Manage/ChangePassword";
    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var state = await db.Users.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.IsActive, x.MustChangePassword }).SingleOrDefaultAsync(context.RequestAborted);
            if (state is null || !state.IsActive) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            if (state.MustChangePassword && !Allowed(context.Request.Path))
            {
                context.Response.Headers.CacheControl = "no-store";
                if (HttpMethods.IsGet(context.Request.Method) && !context.Request.Path.StartsWithSegments("/_blazor"))
                    context.Response.Redirect(ChangePath);
                else context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        await next(context);
    }
    private static bool Allowed(PathString path) => path.Equals(ChangePath, StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/Account/Logout", StringComparison.OrdinalIgnoreCase) || path.Equals("/Error", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content") ||
        path.Equals("/app.css", StringComparison.OrdinalIgnoreCase) || path.Equals("/HRProTools.Web.styles.css", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/images") || path.StartsWithSegments("/lib") || path.StartsWithSegments("/Components") ||
        (path.Value?.Count(c => c == '/') == 1 && new[] { ".css", ".js", ".ico", ".png", ".woff2" }.Contains(Path.GetExtension(path.Value), StringComparer.OrdinalIgnoreCase));
}

public sealed class InitialPasswordService(UserManager<ApplicationUser> users, ApplicationDbContext db)
{
    public async Task<IdentityResult> ChangeAsync(ApplicationUser user, string currentPassword, string newPassword)
    {
        if (newPassword == currentPassword)
            return IdentityResult.Failed(new IdentityError { Code = "PasswordUnchanged", Description = "La nueva contraseña debe ser diferente de la inicial." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var required = user.MustChangePassword;
        user.MustChangePassword = false;
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) { user.MustChangePassword = required; return result; }
        await transaction.CommitAsync();
        return result;
    }
}
