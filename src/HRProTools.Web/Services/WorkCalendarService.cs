using System.ComponentModel.DataAnnotations;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Authorization;
using HRProTools.Web.Security;

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
    bool CountsAsVacationDay,
    Guid? CompanyDayOffRequestId = null);

public sealed record VacationIntervalCalculation(
    Guid EmploymentId,
    DateOnly StartDate,
    DateOnly EndDate,
    int DaysToReserve,
    IReadOnlyList<VacationDateCalculation> Dates);

public sealed record PermissionDateCalculation(
    DateOnly Date,
    Guid WorkScheduleId,
    string WorkScheduleName,
    TimeOnly WorkStart,
    TimeOnly WorkEnd,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    bool IsScheduledWorkday,
    bool IsHoliday,
    string HolidayName,
    bool CountsAsPermissionDay,
    int EffectiveMinutes);

public sealed record PermissionIntervalCalculation(
    Guid EmploymentId,
    string Unit,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int RequestedDays,
    int RequestedMinutes,
    IReadOnlyList<PermissionDateCalculation> Dates);

public sealed class WorkCalendarService(CurrentTenantService currentTenant, TenantDbContextFactory factory, AuthenticationStateProvider authentication)
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
        await RequireAdministratorAsync();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new InvalidOperationException("Indique el nombre del feriado (máximo 200 caracteres).");
        var db = await OpenAsync(cancellationToken);
        await using (db)
        {
            if (!await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, cancellationToken)) throw new UnauthorizedAccessException("Empresa no disponible en este grupo.");
            var existing = await db.CompanyHolidays.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Date == date, cancellationToken);
            if (existing?.IsActive == true) throw new InvalidOperationException("La empresa ya tiene un feriado adicional en esa fecha.");
            if (existing is null) db.CompanyHolidays.Add(new CompanyHoliday { CompanyId = companyId, Date = date, Name = name.Trim() });
            else { existing.IsActive = true; existing.Name = name.Trim(); existing.RemovedAtUtc = null; existing.RemovedByUserId = null; existing.RemovalReason = null; }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveHolidayAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        if (reason.Trim().Length is < 5 or > 500) throw new InvalidOperationException("Indique el motivo de baja (5–500 caracteres).");
        await using var db = await OpenAsync(ct);
        var holiday = await db.CompanyHolidays.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new UnauthorizedAccessException("Feriado adicional no disponible en este grupo.");
        if (!holiday.IsActive) return;
        holiday.IsActive = false; holiday.RemovedAtUtc = DateTimeOffset.UtcNow; holiday.RemovedByUserId = actor; holiday.RemovalReason = reason.Trim();
        await db.SaveChangesAsync(ct);
    }

    private async Task<string> RequireAdministratorAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (!user.IsInRole(AppRoles.TenantAdministrator)) throw new UnauthorizedAccessException("Solo Administrador Tenant puede modificar feriados empresariales.");
        return user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
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
            .Select(x => new { x.Id, x.CompanyId, x.StartDate, x.PersonId })
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
        var holidays = await GlobalReferenceQueries.HolidaysAsync(db, employment.CompanyId, startDate, endDate, cancellationToken);

        var benefits=await db.PermissionRequests.AsNoTracking().Include(x=>x.Dates)
            .Where(x=>x.Employment.PersonId==employment.PersonId&&x.LegalCode==CompanyDayOffService.Code&&
                (x.Status=="PENDING"||x.Status=="APPROVED")&&x.StartDate<=endDate&&x.EndDate>=startDate).ToListAsync(cancellationToken);
        if(benefits.Any(x=>x.Unit=="HOURS"))throw new InvalidOperationException("Existe un permiso parcial de día libre empresarial en este intervalo. Cancele o cambie la solicitud en conflicto antes de presentar vacaciones de jornada completa; no se ajustan saldos automáticamente.");
        if(benefits.Any(x=>x.Status=="PENDING"))throw new InvalidOperationException("Existe un día libre empresarial pendiente en este intervalo. Resuelva o cancele esa solicitud antes de presentar vacaciones.");
        var freeDays=benefits.SelectMany(x=>x.Dates.Where(d=>d.CountsAsPermissionDay).Select(d=>new{d.Date,x.Id})).GroupBy(x=>x.Date).ToDictionary(g=>g.Key,g=>g.First().Id);

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
                holiday ?? (freeDays.ContainsKey(date)?"Día libre empresarial aprobado":""),
                scheduledWorkday && holiday is null && !freeDays.ContainsKey(date),
                freeDays.TryGetValue(date,out var benefitId)?benefitId:null));
        }

        var daysToReserve = dates.Count(x => x.CountsAsVacationDay);
        if (daysToReserve == 0)
            throw new InvalidOperationException("El intervalo no contiene días laborales computables.");
        return new VacationIntervalCalculation(employmentId, startDate, endDate, daysToReserve, dates);
    }

    public static async Task<PermissionIntervalCalculation> CalculatePermissionDaysAsync(
        TenantDbContext db,
        Guid employmentId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var calendar = await ResolvePermissionCalendarAsync(db, employmentId, startDate, endDate, cancellationToken);
        var days = calendar.Count(x => x.CountsAsPermissionDay);
        if (days == 0)
            throw new InvalidOperationException("El intervalo no contiene días laborales computables.");
        return new PermissionIntervalCalculation(
            employmentId,
            PermissionRequestUnits.Days,
            startDate,
            endDate,
            null,
            null,
            days,
            0,
            calendar);
    }

    public static async Task<PermissionIntervalCalculation> CalculatePermissionHoursAsync(
        TenantDbContext db,
        Guid employmentId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken = default)
    {
        if (endTime <= startTime)
            throw new InvalidOperationException("La hora final debe ser posterior a la hora inicial.");
        var calendar = await ResolvePermissionCalendarAsync(db, employmentId, date, date, cancellationToken);
        var day = calendar.Single();
        if (!day.IsScheduledWorkday || day.IsHoliday)
            throw new InvalidOperationException("La fecha seleccionada no corresponde a una jornada laboral aplicable.");
        if (startTime < day.WorkStart || endTime > day.WorkEnd)
            throw new InvalidOperationException($"El permiso debe estar dentro de la jornada {day.WorkStart:HH:mm}–{day.WorkEnd:HH:mm}.");

        var effectiveMinutes = MinutesBetween(startTime, endTime);
        if (day.BreakStart is not null && day.BreakEnd is not null)
        {
            var overlapStart = startTime > day.BreakStart.Value ? startTime : day.BreakStart.Value;
            var overlapEnd = endTime < day.BreakEnd.Value ? endTime : day.BreakEnd.Value;
            if (overlapEnd > overlapStart)
                effectiveMinutes -= MinutesBetween(overlapStart, overlapEnd);
        }
        if (effectiveMinutes <= 0)
            throw new InvalidOperationException("El intervalo coincide solamente con el descanso de la jornada.");

        var snapshot = day with { EffectiveMinutes = effectiveMinutes };
        return new PermissionIntervalCalculation(
            employmentId,
            PermissionRequestUnits.Hours,
            date,
            date,
            startTime,
            endTime,
            0,
            effectiveMinutes,
            [snapshot]);
    }

    internal static async Task<List<PermissionDateCalculation>> ResolvePermissionCalendarAsync(
        TenantDbContext db,
        Guid employmentId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken,
        bool includeClosed = false)
    {
        if (endDate < startDate)
            throw new InvalidOperationException("La fecha final no puede ser anterior a la fecha inicial.");
        var employment = await db.Employments.AsNoTracking()
            .Where(x => x.Id == employmentId && (includeClosed || x.Status == EmploymentStatuses.Active))
            .Select(x => new { x.CompanyId, x.StartDate })
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
        var holidays = await GlobalReferenceQueries.HolidaysAsync(db, employment.CompanyId, startDate, endDate, cancellationToken);

        var result = new List<PermissionDateCalculation>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var effective = assignments.Where(x => x.ValidFrom <= date && (x.ValidTo == null || x.ValidTo >= date)).ToList();
            if (effective.Count != 1)
                throw new InvalidOperationException($"No existe una única jornada vigente para el {date:dd/MM/yyyy}.");
            var schedule = effective[0].WorkSchedule;
            var scheduled = IsScheduledWorkday(schedule, date.DayOfWeek);
            var holiday = holidays.GetValueOrDefault(date);
            var fullDayMinutes = MinutesBetween(schedule.StartTime, schedule.EndTime);
            if (schedule.BreakStart is not null && schedule.BreakEnd is not null)
                fullDayMinutes -= MinutesBetween(schedule.BreakStart.Value, schedule.BreakEnd.Value);
            result.Add(new PermissionDateCalculation(
                date,
                schedule.Id,
                schedule.Name,
                schedule.StartTime,
                schedule.EndTime,
                schedule.BreakStart,
                schedule.BreakEnd,
                scheduled,
                holiday is not null,
                holiday ?? string.Empty,
                scheduled && holiday is null,
                scheduled && holiday is null ? fullDayMinutes : 0));
        }
        return result;
    }

    private static int MinutesBetween(TimeOnly start, TimeOnly end) => (int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes;

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
