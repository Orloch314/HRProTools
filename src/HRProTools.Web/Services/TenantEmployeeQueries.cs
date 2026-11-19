using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public static class TenantEmployeeQueries
{
    public static Task<List<EmployeeRow>> ListAsync(
        TenantDbContext db,
        CancellationToken cancellationToken = default) =>
        db.Employments.AsNoTracking()
            .OrderBy(x => x.EmployeeNumber)
            .Select(x => new EmployeeRow(
                x.Id,
                x.PersonId,
                x.EmployeeNumber,
                x.Person.DocumentNumber,
                x.Person.GivenNames,
                x.Person.FamilyNames,
                x.Person.GivenNames + " " + x.Person.FamilyNames,
                x.Person.Email,
                x.Person.Phone,
                x.CompanyId,
                x.Company.LegalName,
                db.EmploymentAssignments
                    .Where(a => a.EmploymentId == x.Id && a.ValidTo == null)
                    .Select(a => a.DepartmentId)
                    .FirstOrDefault(),
                db.EmploymentAssignments
                    .Where(a => a.EmploymentId == x.Id && a.ValidTo == null)
                    .Select(a => a.Department.Name)
                    .FirstOrDefault() ?? "-",
                db.EmploymentAssignments
                    .Where(a => a.EmploymentId == x.Id && a.ValidTo == null)
                    .Select(a => a.PositionId)
                    .FirstOrDefault(),
                db.EmploymentAssignments
                    .Where(a => a.EmploymentId == x.Id && a.ValidTo == null)
                    .Select(a => a.Position.Name)
                    .FirstOrDefault() ?? "-",
                x.StartDate,
                x.Status,
                db.SalaryHistory
                    .Where(s => s.EmploymentId == x.Id && s.ValidTo == null)
                    .Select(s => s.GrossSalary)
                    .FirstOrDefault(),
                db.EmploymentWorkSchedules
                    .Where(s => s.EmploymentId == x.Id && s.ValidTo == null)
                    .Select(s => s.WorkScheduleId)
                    .FirstOrDefault(),
                db.EmploymentWorkSchedules
                    .Where(s => s.EmploymentId == x.Id && s.ValidTo == null)
                    .Select(s => s.WorkSchedule.Name)
                    .FirstOrDefault() ?? "-",
                "Sin usuario"))
            .ToListAsync(cancellationToken);
}
