using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, bool rolesOnly = false)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                Ensure(await roles.CreateAsync(new IdentityRole(role)));
        }

        if (rolesOnly) return;
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

    public static async Task BootstrapProductionAsync(IServiceProvider services, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new InvalidOperationException("Configure una dirección válida para BootstrapAdmin:Email.");
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        await SeedAsync(services, new ConfigurationBuilder().Build(), rolesOnly: true);
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Administrador global", EmailConfirmed = true, IsActive = true, MustChangePassword = true };
        var db = services.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE auth.\"AspNetUsers\" IN SHARE ROW EXCLUSIVE MODE");
        if (await users.Users.AnyAsync())
            throw new InvalidOperationException("El bootstrap inicial requiere una base sin usuarios. No se sobrescribió ninguna cuenta.");
        Ensure(await users.CreateAsync(user, password));
        Ensure(await users.AddToRoleAsync(user, AppRoles.GlobalAdministrator));
        await transaction.CommitAsync();
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
