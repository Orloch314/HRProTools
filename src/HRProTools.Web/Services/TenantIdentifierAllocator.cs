using System.Globalization;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public static class TenantIdentifierAllocator
{
    public static async Task<string> NextCompanyCodeAsync(
        TenantDbContext db,
        CancellationToken cancellationToken = default)
    {
        await LockAsync(db, "company-code", cancellationToken);
        var existing = await db.Companies.AsNoTracking()
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        return NextPrefixedCode(existing, "EMP");
    }

    public static async Task<string> NextDepartmentCodeAsync(
        TenantDbContext db,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await LockAsync(db, $"department-code:{companyId:N}", cancellationToken);
        var existing = await db.Departments.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        return NextPrefixedCode(existing, "DEP");
    }

    public static async Task<string> NextEmployeeNumberAsync(
        TenantDbContext db,
        CancellationToken cancellationToken = default)
    {
        await LockEmployeeNumbersAsync(db, cancellationToken);
        var existing = await db.Employments.AsNoTracking()
            .Select(x => x.EmployeeNumber)
            .ToListAsync(cancellationToken);

        long highest = 0;
        foreach (var value in existing)
        {
            if (long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > highest)
                highest = number;
        }

        if (highest == long.MaxValue)
            throw new InvalidOperationException("No es posible asignar otro numero de legajo.");

        return (highest + 1).ToString(CultureInfo.InvariantCulture);
    }

    public static Task LockEmployeeNumbersAsync(
        TenantDbContext db,
        CancellationToken cancellationToken = default) =>
        LockAsync(db, "employee-number", cancellationToken);

    private static string NextPrefixedCode(IEnumerable<string> existing, string prefix)
    {
        var marker = prefix + "-";
        var highest = 0;
        foreach (var value in existing)
        {
            if (!value.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                continue;

            if (int.TryParse(value[marker.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > highest)
                highest = number;
        }

        if (highest == int.MaxValue)
            throw new InvalidOperationException("No es posible asignar otro codigo automatico.");

        return $"{prefix}-{highest + 1:D4}";
    }

    private static async Task LockAsync(
        TenantDbContext db,
        string scope,
        CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La asignacion automatica requiere una transaccion activa.");

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext(current_schema() || ':' || {scope}))",
            cancellationToken);
    }
}
