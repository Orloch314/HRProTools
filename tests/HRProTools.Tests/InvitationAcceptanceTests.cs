using System.ComponentModel.DataAnnotations;
using System.Text;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRProTools.Tests;

public sealed class InvitationAcceptanceTests
{
    [Fact]
    public async Task ValidInvitationSetsPasswordConfirmsEmailAndCannotBeReused()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var invitations = scope.ServiceProvider.GetRequiredService<InvitationAcceptanceService>();
        var user = await CreateInvitedUserAsync(users);

        try
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var code = Encode(token);
            const string password = "Invitacion#2026Segura";

            var accepted = await invitations.AcceptAsync(user.Email, code, password);
            Assert.True(accepted.Succeeded, accepted.Message);

            var activated = await users.FindByIdAsync(user.Id);
            Assert.NotNull(activated);
            Assert.True(activated.IsActive);
            Assert.True(activated.EmailConfirmed);
            Assert.NotNull(activated.InvitationAcceptedAtUtc);
            Assert.True(await users.CheckPasswordAsync(activated, password));

            var reused = await invitations.AcceptAsync(user.Email, code, "Otra#Clave2026Segura");
            Assert.False(reused.Succeeded);
            Assert.Contains("ya fue utilizada", reused.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DeleteUserAsync(users, user.Id);
        }
    }

    [Fact]
    public async Task InvalidTokenDoesNotActivateTheAccount()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var invitations = scope.ServiceProvider.GetRequiredService<InvitationAcceptanceService>();
        var user = await CreateInvitedUserAsync(users);

        try
        {
            var result = await invitations.AcceptAsync(user.Email, Encode("token-invalido"), "Invitacion#2026Segura");
            Assert.False(result.Succeeded);

            var unchanged = await users.FindByIdAsync(user.Id);
            Assert.NotNull(unchanged);
            Assert.False(unchanged.IsActive);
            Assert.False(unchanged.EmailConfirmed);
            Assert.Null(unchanged.InvitationAcceptedAtUtc);
            Assert.False(await users.HasPasswordAsync(unchanged));
        }
        finally
        {
            await DeleteUserAsync(users, user.Id);
        }
    }

    [Fact]
    public void DifferentPasswordAndConfirmationAreRejectedInSpanish()
    {
        var model = new AcceptInvitationModel
        {
            Password = "Invitacion#2026Segura",
            ConfirmPassword = "Distinta#2026Segura"
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, result => result.ErrorMessage == "La contraseña y su confirmación no coinciden.");
    }

    private static ServiceProvider CreateProvider()
    {
        var connection = Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Defina HRPROTOOLS_TEST_CONNECTION para la prueba PostgreSQL.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "auth")));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
        services.AddScoped<InvitationAcceptanceService>();
        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateInvitedUserAsync(UserManager<ApplicationUser> users)
    {
        var email = $"invitation-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Invitación técnica",
            IsActive = false,
            EmailConfirmed = false,
            InvitationSentAtUtc = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
        return user;
    }

    private static async Task DeleteUserAsync(UserManager<ApplicationUser> users, string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is not null)
            await users.DeleteAsync(user);
    }

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
}
