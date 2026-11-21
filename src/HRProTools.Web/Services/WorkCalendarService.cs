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

public sealed record VacationDateCalculation(
    DateOnly Date,
    Guid WorkScheduleId,
    string WorkScheduleName,
    bool IsScheduledWorkday,
    bool IsHoliday,
    string HolidayName,
    bool CountsAsVacationDay);

public sealed record VacationIntervalCalculation(
    Guid EmploymentId,
    DateOnly StartDate,
    DateOnly EndDate,
    int DaysToReserve,
    IReadOnlyList<VacationDateCalculation> Dates);

public sealed class WorkCalendarService(CurrentTenantService currentTenant, TenantDbContextFactory factory)
{
    public async Task<List<WorkSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        await using (db)
        {
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
                Name = form.Name.Trim(),
                StartTime = form.StartTime,
                EndTime = form.EndTime,
                BreakStart = form.BreakStart,
                BreakEnd = form.BreakEnd,
                Monday = form.Monday,
                Tuesday = form.Tuesday,
                Wednesday = form.Wednesday,
                Thursday = form.Thursday,
                Friday = form.Friday,
                Saturday = form.Saturday,
                Sunday = form.Sunday
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

    public async Task<VacationIntervalCalculation> CalculateVacationIntervalAsync(
        Guid employmentId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        await using (db)
            return await CalculateVacationIntervalAsync(db, employmentId, startDate, endDate, cancellationToken);
    }

    public static async Task<VacationIntervalCalculation> CalculateVacationIntervalAsync(
        TenantDbContext db,
        Guid employmentId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
            throw new InvalidOperationException("La fecha final no puede ser anterior a la fecha inicial.");

        var employment = await db.Employments.AsNoTracking()
            .Where(x => x.Id == employmentId && x.Status == EmploymentStatuses.Active)
            .Select(x => new { x.Id, x.CompanyId, x.StartDate })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("El vínculo laboral no está activo.");
        if (startDate < employment.StartDate)
            throw new InvalidOperationException("La solicitud no puede comenzar antes del vínculo laboral.");

        var assignments = await db.EmploymentWorkSchedules.AsNoTracking()
            .Include(x => x.WorkSchedule)
            .Where(x => x.EmploymentId == employmentId && x.ValidFrom <= endDate &&
                (x.ValidTo == null || x.ValidTo >= startDate))
            .OrderBy(x => x.ValidFrom)
            .ToListAsync(cancellationToken);
        var holidays = await db.CompanyHolidays.AsNoTracking()
            .Where(x => x.CompanyId == employment.CompanyId && x.Date >= startDate && x.Date <= endDate)
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        var dates = new List<VacationDateCalculation>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var effective = assignments
                .Where(x => x.ValidFrom <= date && (x.ValidTo == null || x.ValidTo >= date))
                .ToList();
            if (effective.Count != 1)
                throw new InvalidOperationException($"No existe una única jornada vigente para el {date:dd/MM/yyyy}.");

            var schedule = effective[0].WorkSchedule;
            var scheduledWorkday = IsScheduledWorkday(schedule, date.DayOfWeek);
            var holiday = holidays.GetValueOrDefault(date);
            dates.Add(new VacationDateCalculation(
                date,
                schedule.Id,
                schedule.Name,
                scheduledWorkday,
                holiday is not null,
                holiday?.Name ?? string.Empty,
                scheduledWorkday && holiday is null));
        }

        var daysToReserve = dates.Count(x => x.CountsAsVacationDay);
        if (daysToReserve == 0)
            throw new InvalidOperationException("El intervalo no contiene días laborales computables.");
        return new VacationIntervalCalculation(employmentId, startDate, endDate, daysToReserve, dates);
    }

    private async Task<TenantDbContext> OpenAsync(CancellationToken ct) => factory.Create((await currentTenant.GetRequiredAsync(ct)).SchemaName);

    private static bool IsScheduledWorkday(WorkSchedule schedule, DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => schedule.Monday,
        DayOfWeek.Tuesday => schedule.Tuesday,
        DayOfWeek.Wednesday => schedule.Wednesday,
        DayOfWeek.Thursday => schedule.Thursday,
        DayOfWeek.Friday => schedule.Friday,
        DayOfWeek.Saturday => schedule.Saturday,
        DayOfWeek.Sunday => schedule.Sunday,
        _ => false
    };

}
