using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRProTools.Web.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=5435;Database=hrprotool;Username=hrprotool_app",
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
            .UseNpgsql("Host=127.0.0.1;Port=5435;Database=hrprotool;Username=hrprotool_app;Search Path=tenant_template")
            .Options;
        return new TenantDbContext(options);
    }
}
