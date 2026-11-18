using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRProTools.Tests;

public sealed class CurrentTenantServiceTests
{
    [Fact]
    public async Task ConcurrentTenantResolutionUsesIndependentGlobalContexts()
    {
        var connection = Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Defina HRPROTOOLS_TEST_CONNECTION para la prueba PostgreSQL.");

        var tenant = new Tenant
        {
            Code = $"CT{Guid.NewGuid():N}"[..12],
            Name = "Tenant de concurrencia",
            SchemaName = $"hr_test_{Guid.NewGuid():N}",
            Status = TenantStatuses.Active
        };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = $"concurrent-{Guid.NewGuid():N}@example.test",
            NormalizedUserName = $"CONCURRENT-{Guid.NewGuid():N}@EXAMPLE.TEST",
            Email = $"concurrent-{Guid.NewGuid():N}@example.test",
            NormalizedEmail = $"CONCURRENT-{Guid.NewGuid():N}@EXAMPLE.TEST",
            DisplayName = "Prueba concurrente",
            TenantId = tenant.Id,
            IsActive = true
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "auth")));
        services.AddSingleton<AuthenticationStateProvider>(
            new FixedAuthenticationStateProvider(user.Id));
        services.AddScoped<CurrentTenantService>();

        await using var provider = services.BuildServiceProvider();
        await using var setupScope = provider.CreateAsyncScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        setupDb.Tenants.Add(tenant);
        setupDb.Users.Add(user);
        await setupDb.SaveChangesAsync();

        try
        {
            await using var circuitScope = provider.CreateAsyncScope();
            var currentTenant = circuitScope.ServiceProvider.GetRequiredService<CurrentTenantService>();
            var results = await Task.WhenAll(
                Enumerable.Range(0, 20).Select(_ => currentTenant.GetRequiredAsync()));

            Assert.All(results, result => Assert.Equal(tenant.Id, result.Id));
        }
        finally
        {
            setupDb.Users.Remove(user);
            setupDb.Tenants.Remove(tenant);
            await setupDb.SaveChangesAsync();
        }
    }

    private sealed class FixedAuthenticationStateProvider(string userId) : AuthenticationStateProvider
    {
        private readonly AuthenticationState state = new(new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                authenticationType: "Test")));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(state);
    }
}
