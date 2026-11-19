using System.ComponentModel.DataAnnotations;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class WorkScheduleForm
{
    [Required, MaxLength(160)] public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; } = new(8, 0);
    public TimeOnly EndTime { get; set; } = new(17, 0);
    public TimeOnly? BreakStart { get; set; } = new(12, 0);
    public TimeOnly? BreakEnd { get; set; } = new(13, 0);
    public bool Monday { get; set; } = true;
    public bool Tuesday { get; set; } = true;
    public bool Wednesday { get; set; } = true;
    public bool Thursday { get; set; } = true;
    public bool Friday { get; set; } = true;
    public bool Saturday { get; set; }
    public bool Sunday { get; set; }
}

public sealed class WorkCalendarService(CurrentTenantService currentTenant, TenantDbContextFactory factory)
{
    public async Task<List<WorkSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        await using (db)
        {
            await EnsureDefaultAssignmentsAsync(db, cancellationToken);
            return await db.WorkSchedules.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        }
    }

    public async Task SaveScheduleAsync(WorkScheduleForm form, CancellationToken cancellationToken = default)
    {
        if (form.EndTime <= form.StartTime || (form.BreakStart is null) != (form.BreakEnd is null) ||
            (form.BreakStart is not null && (form.BreakStart < form.StartTime || form.BreakEnd > form.EndTime || form.BreakEnd <= form.BreakStart)))
            throw new InvalidOperationException("Revise el horario de inicio, finalización y descanso.");
        if (!(form.Monday || form.Tuesday || form.Wednesday || form.Thursday || form.Friday || form.Saturday || form.Sunday))
            throw new InvalidOperationException("Seleccione al menos un día habitual de trabajo.");
        var db = await OpenAsync(cancellationToken);
        await using (db)
        {
            db.WorkSchedules.Add(new WorkSchedule
            {
                Name = form.Name.Trim(), StartTime = form.StartTime, EndTime = form.EndTime,
                BreakStart = form.BreakStart, BreakEnd = form.BreakEnd,
                Monday = form.Monday, Tuesday = form.Tuesday, Wednesday = form.Wednesday,
                Thursday = form.Thursday, Friday = form.Friday, Saturday = form.Saturday, Sunday = form.Sunday
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AssignAsync(Guid employmentId, Guid scheduleId, DateOnly validFrom, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        await using (db)
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (!await db.WorkSchedules.AnyAsync(x => x.Id == scheduleId && x.IsActive, cancellationToken))
                throw new InvalidOperationException("La jornada no está activa.");
            var current = await db.EmploymentWorkSchedules.SingleOrDefaultAsync(x => x.EmploymentId == employmentId && x.ValidTo == null, cancellationToken);
            if (current is not null)
            {
                if (validFrom <= current.ValidFrom) throw new InvalidOperationException("La nueva vigencia debe ser posterior a la jornada actual.");
                current.ValidTo = validFrom.AddDays(-1);
            }
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule { EmploymentId = employmentId, WorkScheduleId = scheduleId, ValidFrom = validFrom });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<List<CompanyHoliday>> GetHolidaysAsync(CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        await using (db) return await db.CompanyHolidays.AsNoTracking().Include(x => x.Company).OrderBy(x => x.Date).ToListAsync(cancellationToken);
    }

    public async Task SaveHolidayAsync(Guid companyId, DateOnly date, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Indique el nombre del feriado.");
        var db = await OpenAsync(cancellationToken);
        await using (db)
        {
            db.CompanyHolidays.Add(new CompanyHoliday { CompanyId = companyId, Date = date, Name = name.Trim() });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<TenantDbContext> OpenAsync(CancellationToken ct) => factory.Create((await currentTenant.GetRequiredAsync(ct)).SchemaName);

    public static async Task EnsureDefaultAssignmentsAsync(TenantDbContext db, CancellationToken ct = default)
    {
        var schedule = await db.WorkSchedules.FirstOrDefaultAsync(x => x.Name == "Jornada estándar", ct);
        if (schedule is null)
        {
            schedule = new WorkSchedule { Name = "Jornada estándar", StartTime = new(8, 0), EndTime = new(17, 0), BreakStart = new(12, 0), BreakEnd = new(13, 0) };
            db.WorkSchedules.Add(schedule);
        }
        var missing = await db.Employments.Where(x => x.Status == EmploymentStatuses.Active && !db.EmploymentWorkSchedules.Any(s => s.EmploymentId == x.Id && s.ValidTo == null)).ToListAsync(ct);
        foreach (var employment in missing)
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule { EmploymentId = employment.Id, WorkSchedule = schedule, ValidFrom = employment.StartDate });
        if (missing.Count > 0 || db.Entry(schedule).State == EntityState.Added) await db.SaveChangesAsync(ct);
    }
}
