namespace HRProTools.Web.Services;

// Numeric parameters shared by the specialized validators and their presentation.
// Specialized changes require a versioned rule change; the global editor cannot alter these parameters.
public static class LegalPermissionLimits
{
    public const int EventDays = 3, PaternityWeeks = 2, MaternityWeeks = 18, SpecialMaternityWeeks = 24;
    public const int AdoptionYoungWeeks = 18, AdoptionOlderWeeks = 12, AdoptionAgeMonths = 6;
    public const int PreventionAnnualDays = 2, PrenatalMinutes = 240, PrenatalNoticeDays = 2;
    public const decimal ObligationMonthDays = 2, ObligationYearDays = 15, DisabilityMonthlyFraction = .5m;
    public const int LactationInitialMinutes = 90, LactationExtensionMinutes = 60, AdditionalChildMinutes = 60;
    public const int LactationInitialAgeMonths = 7, LactationMaximumAgeMonths = 24, CertificateMonths = 3, ReturnMonths = 6;
    public const int SearchDailyMinutes = 120, SearchWeeklyDays = 1, MaximumChildren = 10;
    public static int LactationMinutes(LegalPermissionInput input) => (input.Variant == "EXTENDED" ? LactationExtensionMinutes : LactationInitialMinutes) + AdditionalChildMinutes * (input.Children - 1);
}

public sealed record LegalDurationInfo(string Duration, string Period, string Unit, string Remuneration, string Review, IReadOnlyList<string> Conditions)
{
    public static LegalDurationInfo From(LegalPermissionType rule)
    {
        string duration, period, unit;
        List<string> conditions = [];
        if(rule.Engine=="COMPANY_BENEFIT")
        {
            duration=$"{rule.AnnualLimit:0.0} jornada contractual";period="Una solicitud por persona y año civil, sin acumulación";unit="Jornadas";
            conditions.Add("Política de la empresa vigente en la fecha de disfrute; no se suma a vacaciones. Una media jornada conserva sus minutos efectivos y requiere resolver conflictos con vacaciones completas.");
        }
        else if (rule.Engine == "PARAMETRIC")
        {
            var quantityUnit = UnitName(rule.QuantityUnit);
            unit = UnitName(rule.Unit);
            duration = rule.MaximumQuantity.HasValue ? $"Hasta {rule.MaximumQuantity:0.###} {quantityUnit.ToLowerInvariant()} por solicitud" : "Según duración solicitada y circuito";
            period = "Según los límites publicados";
            if (rule.EventLimit.HasValue) conditions.Add($"Por evento: {rule.EventLimit:0.###} {quantityUnit.ToLowerInvariant()}.");
            if (rule.MonthlyLimit.HasValue) conditions.Add($"Por mes: {rule.MonthlyLimit:0.###} {quantityUnit.ToLowerInvariant()}.");
            if (rule.AnnualLimit.HasValue) conditions.Add($"Por año: {rule.AnnualLimit:0.###} {quantityUnit.ToLowerInvariant()}.");
            if (!rule.EventLimit.HasValue && !rule.MonthlyLimit.HasValue && !rule.AnnualLimit.HasValue) period = "Sin cupo periódico adicional publicado";
            if (rule.Unit == "DAYS_OR_HOURS") conditions.Add("Admite días u horas; los límites se expresan en fracciones de la jornada de cada fecha.");
        }
        else
        {
            unit = "Días"; period = "Según condición legal";
            switch (rule.Code)
            {
                case "MATRIMONIO": case "LUTO_FAMILIAR": duration = $"{LegalPermissionLimits.EventDays} días"; period = "Por evento"; conditions.Add("HR verifica el cómputo y las fechas del evento; no se trasladan automáticamente fines de semana ni feriados."); break;
                case "PATERNIDAD": duration = $"{LegalPermissionLimits.PaternityWeeks} semanas consecutivas"; unit = "Semanas"; period = "Por nacimiento"; break;
                case "MATERNIDAD": duration = $"{LegalPermissionLimits.MaternityWeeks} semanas ordinarias"; unit = "Semanas"; period = "Por evento"; conditions.Add($"{LegalPermissionLimits.SpecialMaternityWeeks} semanas en supuestos especiales; un mes adicional por hijo desde el segundo. Se aplica la mayor extensión, sin sumar indiscriminadamente los supuestos."); conditions.Add("Transferencia: según remanente del período original certificado."); break;
                case "ADOPCION": duration = $"{LegalPermissionLimits.AdoptionYoungWeeks} o {LegalPermissionLimits.AdoptionOlderWeeks} semanas consecutivas"; unit = "Semanas"; period = "Por resolución judicial"; conditions.Add($"{LegalPermissionLimits.AdoptionYoungWeeks} semanas para menor de {LegalPermissionLimits.AdoptionAgeMonths} meses; {LegalPermissionLimits.AdoptionOlderWeeks} para mayor. Edad exacta: revisión HR."); break;
                case "PAP_MAMOGRAFIA": case "PREVENCION_PROSTATA_TESTICULOS_PENE_COLON": duration = $"{LegalPermissionLimits.PreventionAnnualDays} días laborales"; period = "Por año calendario, por persona"; conditions.Add("Cupo compartido entre los exámenes de esta categoría, no por examen. Las categorías legales mantienen cupos separados."); break;
                case "CONTROL_PRENATAL": duration = $"Hasta {LegalPermissionLimits.PrenatalMinutes / 60} horas por control"; unit = "Horas"; period = "Por control"; conditions.Add("Ampliación según duración certificada; sin cupo anual genérico."); conditions.Add($"Preaviso de {LegalPermissionLimits.PrenatalNoticeDays} días salvo urgencia."); break;
                case "ACOMPANAMIENTO_DISCAPACIDAD": duration = $"Hasta {LegalPermissionLimits.DisabilityMonthlyFraction:P0} de la jornada laboral mensual aplicable"; unit = "Horas"; period = "Por mes"; conditions.Add("El límite se calcula con la jornada y el calendario aplicables; no es una cantidad fija de horas."); break;
                case "OBLIGACION_LEGAL": duration = $"Hasta {LegalPermissionLimits.ObligationMonthDays:0} días mensuales"; period = "Mensual y anual"; conditions.Add($"Máximo {LegalPermissionLimits.ObligationYearDays:0} días anuales; las horas se convierten a fracciones de jornada."); break;
                case "LACTANCIA": duration = $"{LegalPermissionLimits.LactationInitialMinutes} minutos diarios iniciales"; unit = "Minutos"; period = "Por día laborable dentro del período aplicable"; conditions.Add($"Extensión: {LegalPermissionLimits.LactationExtensionMinutes} minutos diarios; +{LegalPermissionLimits.AdditionalChildMinutes} por hijo desde el segundo."); conditions.Add($"Inicial hasta {LegalPermissionLimits.LactationInitialAgeMonths} meses de edad; supuesto desde reintegro sujeto a revisión HR. Extensiones hasta {LegalPermissionLimits.LactationMaximumAgeMonths} meses con certificado renovable cada {LegalPermissionLimits.CertificateMonths} meses."); break;
                case "BUSQUEDA_EMPLEO": duration = $"{LegalPermissionLimits.SearchDailyMinutes / 60} horas diarias o {LegalPermissionLimits.SearchWeeklyDays} día semanal"; unit = "Horas o días"; period = "Dentro del preaviso verificado"; conditions.Add("Modalidades exclusivas: no pueden combinarse. No se acumulan automáticamente los días."); break;
                case "REPOSO_MEDICO": duration = "Según duración certificada"; period = "Según condición médica"; conditions.Add("Se comunica en Mis ausencias; no se presenta como otro permiso."); break;
                default: duration = rule.Duration; unit = UnitName(rule.Unit); break;
            }
        }
        var remuneration = rule.Code is "MATERNIDAD" or "ADOPCION" or "REPOSO_MEDICO" ? rule.Remuneration : rule.IsPaid ? "Con goce de salario" : "Sin goce de salario";
        return new(duration, period, unit, remuneration, rule.Protected ? "Recursos Humanos" : "Circuito de autorización configurado", conditions);
    }
    public static string UnitName(string unit) => unit switch { "REQUESTS" => "Solicitudes", "DAYS" or "WORKDAYS" or "EVENT_DAYS" or "CALENDAR_DAYS" => "Días", "HOURS" => "Horas", "MINUTES" or "RECURRENT_MINUTES" => "Minutos", "WEEKS" => "Semanas", "DAYS_OR_HOURS" or "RECURRENT" => "Días u horas", "ABSENCE" => "Según constancia", _ => "Según condición legal" };
}
