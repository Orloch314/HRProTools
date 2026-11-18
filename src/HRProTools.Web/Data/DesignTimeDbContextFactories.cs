using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRProTools.Web.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=hrprotools;Username=hrprotools_app",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "auth"))
            .Options;
        return new ApplicationDbContext(options);
    }
}

public sealed class TenantDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql("Host=localhost;Database=hrprotools;Username=hrprotools_app;Search Path=tenant_template")
            .Options;
        return new TenantDbContext(options);
    }
}
