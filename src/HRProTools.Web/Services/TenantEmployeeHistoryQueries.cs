using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record EmployeeGeneralData(
    Guid PersonId,
    Guid EmploymentId,
    string EmployeeNumber,
    string DocumentNumber,
    string GivenNames,
    string FamilyNames,
    string FullName,
    string Email,
    string Phone,
    string Company,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    string? Sex);

public sealed record LaborHistoryRow(
    Guid EmploymentId,
    string EmployeeNumber,
    string Company,
    string Department,
    string Position,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    bool IsCurrent);

public sealed record SalaryHistoryRow(
    Guid EmploymentId,
    string EmployeeNumber,
    string Company,
    decimal GrossSalary,
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsCurrent);

public sealed record EmployeeHistoryProfile(
    EmployeeGeneralData General,
    IReadOnlyList<LaborHistoryRow> LaborHistory);

public static class TenantEmployeeHistoryQueries
{
    public static async Task<EmployeeHistoryProfile?> GetProfileAsync(
        TenantDbContext db,
        Guid employmentId,
        CancellationToken cancellationToken = default)
    {
        var general = await db.Employments.AsNoTracking()
            .Where(x => x.Id == employmentId)
            .Select(x => new EmployeeGeneralData(
                x.PersonId,
                x.Id,
                x.EmployeeNumber,
                x.Person.DocumentNumber,
                x.Person.GivenNames,
                x.Person.FamilyNames,
                x.Person.GivenNames + " " + x.Person.FamilyNames,
                x.Person.Email,
                x.Person.Phone,
                x.Company.LegalName,
                x.StartDate,
                x.EndDate,
                x.Status,
                x.Person.Sex))
            .SingleOrDefaultAsync(cancellationToken);

        if (general is null)
            return null;

        var laborHistory = await db.EmploymentAssignments.AsNoTracking()
            .Where(x => x.Employment.PersonId == general.PersonId)
            .OrderByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.Employment.StartDate)
            .Select(x => new LaborHistoryRow(
                x.EmploymentId,
                x.Employment.EmployeeNumber,
                x.Employment.Company.LegalName,
                x.Department.Name,
                x.Position.Name,
                x.ValidFrom,
                x.ValidTo,
                x.Reason,
                x.ValidTo == null && x.Employment.Status == EmploymentStatuses.Active))
            .ToListAsync(cancellationToken);

        return new EmployeeHistoryProfile(general, laborHistory);
    }

    public static async Task<List<SalaryHistoryRow>> GetSalaryHistoryAsync(
        TenantDbContext db,
        Guid employmentId,
        CancellationToken cancellationToken = default)
    {
        var personId = await db.Employments.AsNoTracking()
            .Where(x => x.Id == employmentId)
            .Select(x => (Guid?)x.PersonId)
            .SingleOrDefaultAsync(cancellationToken);

        if (personId is null)
            return [];

        return await db.SalaryHistory.AsNoTracking()
            .Where(x => x.Employment.PersonId == personId.Value)
            .OrderByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.Employment.StartDate)
            .Select(x => new SalaryHistoryRow(
                x.EmploymentId,
                x.Employment.EmployeeNumber,
                x.Employment.Company.LegalName,
                x.GrossSalary,
                "PYG",
                x.ValidFrom,
                x.ValidTo,
                x.ValidTo == null && x.Employment.Status == EmploymentStatuses.Active))
            .ToListAsync(cancellationToken);
    }
}
