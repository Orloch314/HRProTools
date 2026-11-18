using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRProTools.Web.Services;

public sealed class TenantDbContextFactory(IConfiguration configuration)
{
    public TenantDbContext Create(string schemaName)
    {
        if (!TenantSchemaNames.IsValid(schemaName))
            throw new InvalidOperationException("El esquema tenant no es valido.");

        var baseConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection.");
        var connection = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schemaName };
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connection.ConnectionString)
            .Options;
        return new TenantDbContext(options);
    }
}

public static class TenantSchemaNames
{
    public static string For(Guid tenantId) => $"hr_t_{tenantId:N}";

    public static bool IsValid(string value) =>
        value.Length == 37 &&
        value.StartsWith("hr_t_", StringComparison.Ordinal) &&
        value[5..].All(Uri.IsHexDigit);
}
