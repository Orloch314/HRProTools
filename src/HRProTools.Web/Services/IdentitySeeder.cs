using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Identity;

namespace HRProTools.Web.Services;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                Ensure(await roles.CreateAsync(new IdentityRole(role)));
        }

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email.Trim().ToLowerInvariant(),
                Email = email.Trim().ToLowerInvariant(),
                DisplayName = "Administrador global",
                EmailConfirmed = true,
                IsActive = true
            };
            Ensure(await users.CreateAsync(admin, password));
        }

        if (!await users.IsInRoleAsync(admin, AppRoles.GlobalAdministrator))
            Ensure(await users.AddToRoleAsync(admin, AppRoles.GlobalAdministrator));
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
