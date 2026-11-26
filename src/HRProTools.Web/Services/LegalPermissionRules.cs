using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public static class LegalPermissionRules
{
    public static DateOnly MaternityEnd(DateOnly start, string variant, int children)
    {
        if (children is < 1 or > LegalPermissionLimits.MaximumChildren) throw new InvalidOperationException("Cantidad de hijos inválida.");
        if (variant is not "ORDINARY" and not "SPECIAL" and not "MULTIPLE_SPECIAL") throw new InvalidOperationException("Supuesto de maternidad inválido.");
        var ordinary = start.AddDays(LegalPermissionLimits.MaternityWeeks * 7).AddMonths(children - 1).AddDays(-1);
        var special = start.AddDays(LegalPermissionLimits.SpecialMaternityWeeks * 7 - 1);
        return variant == "ORDINARY" ? ordinary : (ordinary > special ? ordinary : special);
    }

    public static async Task<PermissionIntervalCalculation> CalculateAsync(TenantDbContext db, Guid employmentId,
        LegalPermissionInput input, string unit, DateOnly start, DateOnly end, TimeOnly? from, TimeOnly? to, CancellationToken ct, LegalPermissionType? published = null)
    {
        var type = published ?? LegalPermissionCatalog.Get(input.Code);
        if (end < start || end.DayNumber - start.DayNumber > 800) throw new InvalidOperationException("Revise el período de la solicitud (máximo 800 días por registro).");
        if(type.Engine=="COMPANY_BENEFIT") return await CompanyDayOffService.CalculateAsync(db,employmentId,type,input,unit,start,end,ct);
        if (type.Engine == "PARAMETRIC") return await ParametricPermissionRules.CalculateAsync(db, employmentId, type, unit, start, end, from, to, ct);
        if (type.Unit == "ABSENCE") throw new InvalidOperationException("Comunique el reposo desde Mis ausencias; no se duplica como permiso.");
        if (input.Relationship.Length > 40 || input.Exam.Length > 40 || input.ObligationReference.Length > 500) throw new InvalidOperationException("Datos del supuesto demasiado largos.");
        var calendar = await WorkCalendarService.ResolvePermissionCalendarAsync(db, employmentId, start, end, ct);
        if (input.Code is "MATRIMONIO" or "LUTO_FAMILIAR")
        {
            if (unit != PermissionRequestUnits.Days) throw new InvalidOperationException("Registre este permiso por días.");
            if (input.Code == "LUTO_FAMILIAR" && input.Relationship is not "CONYUGE" and not "HIJO" and not "PADRE_MADRE" and not "ABUELO" and not "HERMANO") throw new InvalidOperationException("Parentesco no contemplado en este beneficio.");
            // Optional explicitly chosen dates avoid imposing an unsupported weekend/holiday convention.
            if (input.EventSecondDay.HasValue || input.EventThirdDay.HasValue)
            {
                var chosen = new[] { (DateOnly?)start, input.EventSecondDay, input.EventThirdDay }.Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
                if (chosen.Count != LegalPermissionLimits.EventDays || chosen.Min() != start || chosen.Max() != end) throw new InvalidOperationException("Seleccione tres fechas distintas dentro del período indicado, cuya revisión corresponde a HR.");
                return new(employmentId, unit, start, end, null, null, LegalPermissionLimits.EventDays, 0, calendar.Select(x => x with { CountsAsPermissionDay = chosen.Contains(x.Date), EffectiveMinutes = chosen.Contains(x.Date) ? x.EffectiveMinutes : 0 }).ToList());
            }
            if (end.DayNumber - start.DayNumber + 1 > LegalPermissionLimits.EventDays) throw new InvalidOperationException("El registro por evento no puede superar tres días; HR debe verificar el cómputo del período.");
            return Days(employmentId, start, end, calendar, true);
        }
        if (input.Code == "PATERNIDAD")
        {
            if (unit != PermissionRequestUnits.Days || start != input.EventDate || end != input.EventDate.AddDays(LegalPermissionLimits.PaternityWeeks * 7 - 1)) throw new InvalidOperationException("Paternidad: registre las dos semanas posteriores al nacimiento, sin sumar los antiguos tres días.");
            return Days(employmentId, start, end, calendar, true);
        }
        if (input.Code == "MATERNIDAD")
        {
            DateOnly maximum;
            if (input.Variant == "TRANSFER")
            {
                if (input.OriginalStart is null || input.OriginalEnd is null || input.DeathDate is null || input.OriginalEnd < input.OriginalStart || start < input.DeathDate || start < input.OriginalStart || end > input.OriginalEnd)
                    throw new InvalidOperationException("La transferencia requiere período original, fallecimiento y remanente coherentes, sujetos a verificación HR.");
                maximum = input.OriginalEnd.Value;
            }
            else { if (start < input.EventDate.AddDays(-14)) throw new InvalidOperationException("Revise certificado y fecha probable de parto: inicio prenatal hasta dos semanas antes."); maximum = MaternityEnd(start, input.Variant, input.Children); }
            if (unit != PermissionRequestUnits.Days || end != maximum) throw new InvalidOperationException($"El supuesto indicado finaliza el {maximum:dd/MM/yyyy}; revisar certificado.");
            return Days(employmentId, start, end, calendar, true);
        }
        if (input.Code == "ADOPCION")
        {
            if (input.ChildBirthDate is null || input.ChildBirthDate > input.EventDate || start < input.EventDate) throw new InvalidOperationException("Indique nacimiento y fecha de resolución judicial coherentes.");
            var sixMonths = input.ChildBirthDate.Value.AddMonths(LegalPermissionLimits.AdoptionAgeMonths);
            var weeks = input.EventDate < sixMonths ? LegalPermissionLimits.AdoptionYoungWeeks : input.EventDate > sixMonths ? LegalPermissionLimits.AdoptionOlderWeeks : input.Variant == "SIX_MONTH_REVIEW_18" ? LegalPermissionLimits.AdoptionYoungWeeks : LegalPermissionLimits.AdoptionOlderWeeks;
            if (unit != PermissionRequestUnits.Days || end != start.AddDays(weeks * 7 - 1)) throw new InvalidOperationException("El período debe corresponder a la duración para la edad indicada.");
            return Days(employmentId, start, end, calendar, true);
        }
        if (input.Code is "LACTANCIA" or "BUSQUEDA_EMPLEO")
        {
            if (unit != PermissionRequestUnits.Hours && !(input.Code == "BUSQUEDA_EMPLEO" && unit == PermissionRequestUnits.Days)) throw new InvalidOperationException("Seleccione la modalidad recurrente adecuada.");
            if (input.Code == "LACTANCIA")
            {
                if (input.ChildBirthDate is null || input.ReturnDate is null || input.ChildBirthDate > input.ReturnDate || start < input.ReturnDate || end >= input.ChildBirthDate.Value.AddMonths(LegalPermissionLimits.LactationMaximumAgeMonths) || input.Children is < 1 or > LegalPermissionLimits.MaximumChildren) throw new InvalidOperationException("Indique nacimiento, reintegro, período dentro de los 24 meses de edad y cantidad de hijos coherentes.");
                var extension = input.Variant == "EXTENDED";
                if (input.Variant is not "ORDINARY" and not "EXTENDED" and not "INITIAL_HR_REVIEW") throw new InvalidOperationException("Modalidad de lactancia inválida.");
                // MTESS operational guidance under Decreto 7550/2017 uses seven months of age.
                // The statute wording is retained in the snapshot; a differing case needs explicit HR legal review.
                var limit = extension ? input.ChildBirthDate.Value.AddMonths(LegalPermissionLimits.LactationMaximumAgeMonths).AddDays(-1) : input.Variant == "INITIAL_HR_REVIEW" ? input.ReturnDate.Value.AddMonths(LegalPermissionLimits.ReturnMonths).AddDays(-1) : input.ChildBirthDate.Value.AddMonths(LegalPermissionLimits.LactationInitialAgeMonths).AddDays(-1);
                if (extension && (input.MedicalCertificateDate is null || start < input.MedicalCertificateDate || end >= input.MedicalCertificateDate.Value.AddMonths(LegalPermissionLimits.CertificateMonths))) throw new InvalidOperationException("La extensión debe quedar cubierta por un certificado vigente (renovación a los tres meses según guía MTESS).");
                var minutes = LegalPermissionLimits.LactationMinutes(input);
                if (extension && !input.MedicalExtension || end > limit || input.DailyMinutes != minutes) throw new InvalidOperationException($"Revise período y duración: {minutes} minutos diarios; extensión con indicación médica.");
            }
            else
            {
                var notice = await db.EmploymentNotices.AsNoTracking().SingleOrDefaultAsync(x => x.EmploymentId == employmentId && x.StartDate <= start && x.EndDate >= end, ct)
                    ?? throw new InvalidOperationException("HR debe verificar previamente el preaviso de despido aplicable.");
                if (notice.Mode != unit) throw new InvalidOperationException("No puede combinar modalidades durante el preaviso verificado.");
                if (unit == PermissionRequestUnits.Days)
                {
                    var days = Days(employmentId, start, end, calendar, false);
                    if (days.RequestedDays > LegalPermissionLimits.SearchWeeklyDays) throw new InvalidOperationException("Seleccione un día por semana; no se acumulan automáticamente días.");
                    return days;
                }
                if (input.DailyMinutes != LegalPermissionLimits.SearchDailyMinutes) throw new InvalidOperationException("La modalidad diaria es de dos horas.");
            }
            if (from is null) throw new InvalidOperationException("Indique hora inicial diaria.");
            var dates = new List<PermissionDateCalculation>();
            foreach (var day in calendar)
            {
                if (!day.CountsAsPermissionDay) { dates.Add(day with { EffectiveMinutes = 0 }); continue; }
                var dailyEnd = from.Value.AddMinutes(input.DailyMinutes);
                if (dailyEnd <= from) throw new InvalidOperationException("El permiso no puede atravesar medianoche.");
                var daily = await WorkCalendarService.CalculatePermissionHoursAsync(db, employmentId, day.Date, from.Value, dailyEnd, ct);
                if (daily.RequestedMinutes != input.DailyMinutes) throw new InvalidOperationException("El intervalo diario debe cubrir la duración legal sin incluir el descanso.");
                dates.Add(daily.Dates.Single());
            }
            var total = dates.Sum(x => x.EffectiveMinutes);
            if (total == 0) throw new InvalidOperationException("No hay jornada computable.");
            return new(employmentId, unit, start, end, from, from.Value.AddMinutes(input.DailyMinutes), 0, total, dates);
        }
        PermissionIntervalCalculation calculation;
        if (unit == PermissionRequestUnits.Hours && from.HasValue && to.HasValue && start == end)
            calculation = await WorkCalendarService.CalculatePermissionHoursAsync(db, employmentId, start, from.Value, to.Value, ct);
        else if (unit == PermissionRequestUnits.Days)
            calculation = await WorkCalendarService.CalculatePermissionDaysAsync(db, employmentId, start, end, ct);
        else throw new InvalidOperationException("Indique fechas, unidad y horario válidos.");
        if (input.Code == "CONTROL_PRENATAL")
        {
            if (unit != PermissionRequestUnits.Hours || calculation.RequestedMinutes > LegalPermissionLimits.PrenatalMinutes && !input.MedicalExtension) throw new InvalidOperationException("Más de cuatro horas requiere aval médico para la ampliación.");
            var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Asuncion");
            if (!input.Urgent && start.ToDateTime(from!.Value) < now.DateTime.AddDays(LegalPermissionLimits.PrenatalNoticeDays)) throw new InvalidOperationException("Indique urgencia o respete el preaviso ordinario de dos días.");
        }
        if (input.Code is "PAP_MAMOGRAFIA" or "PREVENCION_PROSTATA_TESTICULOS_PENE_COLON")
        {
            if (unit != PermissionRequestUnits.Days) throw new InvalidOperationException("Beneficio computado en días laborales.");
            var exams = input.Code == "PAP_MAMOGRAFIA" ? new[] { "PAP", "MAMOGRAFIA", "AMBOS" } : ["PROSTATA", "TESTICULOS", "PENE", "COLON"];
            if (!exams.Contains(input.Exam)) throw new InvalidOperationException("Seleccione un examen contemplado; no registre diagnósticos.");
        }
        if (input.Code == "ACOMPANAMIENTO_DISCAPACIDAD" && unit != PermissionRequestUnits.Hours) throw new InvalidOperationException("Registre las horas de acompañamiento.");
        if (input.Code == "OBLIGACION_LEGAL" && string.IsNullOrWhiteSpace(input.ObligationReference)) throw new InvalidOperationException("Indique la obligación legal o disposición gubernativa.");
        return calculation;
    }

    private static PermissionIntervalCalculation Days(Guid id, DateOnly start, DateOnly end, IReadOnlyList<PermissionDateCalculation> dates, bool calendar) =>
        new(id, PermissionRequestUnits.Days, start, end, null, null, calendar ? end.DayNumber - start.DayNumber + 1 : dates.Count(x => x.CountsAsPermissionDay), 0,
            dates.Select(x => calendar ? x with { CountsAsPermissionDay = true } : x).ToList());

    public static async Task CheckQuotasAsync(TenantDbContext db, Guid employmentId, LegalPermissionInput input, PermissionIntervalCalculation calculation, CancellationToken ct, LegalPermissionType? published = null)
    {
        if (published?.Engine == "PARAMETRIC") { await ParametricPermissionRules.CheckAsync(db, employmentId, published, input, calculation, ct); return; }
        var rule = published ?? LegalPermissionCatalog.Get(input.Code);
        var balance = await PermissionQuotaQueries.ReadAsync(db, employmentId, rule, input, calculation.StartDate, calculation.EndDate, candidate: calculation, ct: ct);
        PermissionQuotaQueries.EnsureAvailable(balance);
    }
    public static int FullMinutes(PermissionDateCalculation day) => (int)(day.WorkEnd - day.WorkStart).TotalMinutes -
        (day.BreakStart.HasValue && day.BreakEnd.HasValue ? (int)(day.BreakEnd.Value - day.BreakStart.Value).TotalMinutes : 0);
}
