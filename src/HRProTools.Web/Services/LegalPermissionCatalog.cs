namespace HRProTools.Web.Services;

public sealed record LegalPermissionType(string Code, string Name, string Group, string Law, string Article,
    string Duration, string Unit, string Beneficiaries, string Requirements, string Documentation,
    string Notice, string Remuneration, bool Protected = true, bool Medical = false)
{
    // This is the effective date of the software rule version, not a fabricated law commencement date.
    public string RuleVersion { get; init; } = "PY-2026-11-23-v1";
    public DateOnly RuleEffectiveFrom { get; init; } = new(2026, 11, 23);
    public DateOnly? RuleEffectiveTo { get; init; }
    public string Description { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public string Engine { get; init; } = "SPECIAL";
    public string QuantityUnit { get; init; } = "DAYS";
    public decimal? MaximumQuantity { get; init; }
    public decimal? EventLimit { get; init; }
    public decimal? MonthlyLimit { get; init; }
    public decimal? AnnualLimit { get; init; }
    public bool IsPaid { get; init; } = true;
    public Guid? CompanyPolicyId { get; init; }
    public Guid? PolicyCompanyId { get; init; }
}

public static class LegalPermissionCatalog
{
    public static readonly IReadOnlyList<LegalPermissionType> All = [
        new("MATRIMONIO", "Matrimonio", "Familiares", "3384/2007", "1; Código Laboral 62(j)", "3 días por evento; cómputo sujeto a revisión HR", "EVENT_DAYS", "Trabajador que contrae matrimonio", "Fecha y constancia del evento", "Constancia de matrimonio", "Sin plazo específico verificado", "Salario a cargo del empleador"),
        new("LUTO_FAMILIAR", "Fallecimiento de familiar", "Familiares", "3384/2007", "1; Código Laboral 62(j)", "3 días por evento; cómputo sujeto a revisión HR", "EVENT_DAYS", "Cónyuge, hijos, padres, abuelos o hermanos", "Parentesco contemplado; admite comunicación posterior", "Defunción y acreditación de parentesco", "Admite imprevistos", "Salario a cargo del empleador"),
        new("PATERNIDAD", "Paternidad", "Maternidad, paternidad y adopción", "5508/2015", "13(b)", "2 semanas posteriores al parto", "CALENDAR_DAYS", "Padre del recién nacido", "Nacimiento e inscripción", "Constancia de nacimiento e inscripción", "Relacionado con nacimiento", "Salario a cargo del empleador"),
        new("MATERNIDAD", "Maternidad", "Maternidad, paternidad y adopción", "5508/2015; 7534/2025", "11; modificatoria art. 1", "18 semanas; 24 especiales; meses adicionales por parto múltiple; mayor extensión, sin suma indiscriminada", "CALENDAR_DAYS", "Trabajadora; transferencia por fallecimiento a padre/cuidador", "Supuesto y período certificados; transferencia limitada al remanente", "Certificado IPS/MSPBS; documentación del supuesto especial", "Posible inicio 2 semanas antes del parto", "Régimen legal de subsidio; sin cálculo IPS/payroll", Medical: true),
        new("ADOPCION", "Adopción o acogimiento judicial", "Maternidad, paternidad y adopción", "5508/2015", "13(a)", "18 semanas menor de 6 meses; 12 mayor de 6 meses", "CALENDAR_DAYS", "Madre adoptante o de acogimiento con sentencia", "Edad y resolución judicial; exactamente 6 meses requiere revisión", "Sentencia judicial", "Según resolución", "Régimen legal aplicable, sin cálculo de subsidios"),
        new("LACTANCIA", "Lactancia", "Lactancia", "5508/2015", "14", "90 min/día iniciales; 60 min extensión médica hasta 24 meses; +60 por hijo desde el segundo", "RECURRENT_MINUTES", "Madre trabajadora; excepciones revisadas por HR", "Reintegro, nacimiento, hijos y modalidad; dentro de jornada", "Nacimiento, reintegro e indicación médica para extensión", "Solicitud inicial única", "Tiempo trabajado con salario", Medical: true),
        new("CONTROL_PRENATAL", "Control prenatal", "Salud preventiva", "7383/2024", "4-6", "Hasta 4 horas por control, ampliables con aval médico", "HOURS", "Trabajadora gestante; revisión HR", "Preaviso 2 días salvo urgencia; sin cupo anual", "Constancia posterior; aval médico para ampliación", "2 días; urgencia exceptuada", "Tiempo trabajado remunerado", Medical: true),
        new("PAP_MAMOGRAFIA", "Papanicolau y mamografía", "Salud preventiva", "3803/2009; 6211/2018", "1 modificado; 2", "2 días laborales por año en conjunto", "WORKDAYS", "Trabajadora; circunstancias excepcionales a HR", "Examen y cupo anual único", "Constancia del examen", "Coordinar; sin plazo adicional inventado", "Licencia remunerada", Medical: true),
        new("PREVENCION_PROSTATA_TESTICULOS_PENE_COLON", "Prevención de próstata, testículos, pene o colon", "Salud preventiva", "6280/2019; 7659/2026", "13 modificado", "2 días laborales por año en conjunto", "WORKDAYS", "Trabajador; colon sin reducción por sexo", "Examen contemplado; no multiplica cupo por órgano", "Constancia de examen, sin diagnóstico", "Sin plazo específico verificado", "Licencia remunerada", Medical: true),
        new("ACOMPANAMIENTO_DISCAPACIDAD", "Acompañamiento de niño o adolescente con discapacidad", "Otros permisos habilitados", "7371/2025", "17", "Hasta 50% de jornada laboral mensual total", "HOURS", "Madre, padre o persona cuidadora", "Rol de cuidado y atención necesaria; cupo por mes/jornada", "Acreditación de cuidado y atención; sin diagnóstico detallado", "Según atención", "Con goce de sueldo", Medical: true),
        new("OBLIGACION_LEGAL", "Obligación legal o gubernativa", "Obligaciones legales", "213/1993", "62(h)", "2 días/mes y 15 días/año remunerados", "DAYS_OR_HOURS", "Trabajador obligado legalmente", "Obligación respaldada; fracciones según jornada de cada día", "Citación o disposición", "Según obligación", "Remunerado dentro de límites"),
        new("BUSQUEDA_EMPLEO", "Búsqueda de empleo durante preaviso de despido", "Obligaciones legales", "213/1993", "87-89", "2 horas/día o 1 día/semana, sin combinar modalidades", "RECURRENT", "Trabajador notificado de despido con preaviso", "Período y modalidad verificados por HR", "Notificación escrita del preaviso", "Dentro del preaviso", "Sin disminución salarial"),
        new("REPOSO_MEDICO", "Comunicar reposo médico", "Otros permisos habilitados", "Régimen de ausencias", "Según documentación", "Se comunica en Mis ausencias", "ABSENCE", "Funcionario", "Flujo existente de ausencia médica", "Documento revisado por HR", "Según comunicación", "Sin asignar 100% ni calcular subsidio", Medical: true),
        new("PERMISO_SIN_GOCE", "Permiso ordinario sin goce", "Otros permisos habilitados", "Circuito ordinario", "No constituye derecho legal tipificado", "Por días u horas", "DAYS_OR_HOURS", "Funcionario", "Sujeto al circuito configurable", "Según revisión", "Según solicitud", "Sin goce; sin descuentos automáticos", Protected: false),
        new("PERMISO_ORDINARIO_CON_GOCE", "Permiso ordinario con goce", "Otros permisos habilitados", "Compatibilidad con permisos existentes", "No constituye catálogo legal adicional", "Por días u horas", "DAYS_OR_HOURS", "Funcionario", "Conserva funcionalidad genérica existente", "Según revisión", "Según solicitud", "Con goce sujeto a aprobación", Protected: false)
    ];
    public static LegalPermissionType Get(string code) => All.SingleOrDefault(x => x.Code == code)
        ?? throw new InvalidOperationException("Tipo legal no habilitado.");
}

public sealed record LegalPermissionInput
{
    public string Code { get; set; } = "MATRIMONIO";
    public DateOnly EventDate { get; set; } = GlobalReferenceQueries.Today;
    public string Relationship { get; set; } = "";
    public string Exam { get; set; } = "";
    public string Variant { get; set; } = "ORDINARY";
    public int Children { get; set; } = 1;
    public DateOnly? ChildBirthDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public bool MedicalExtension { get; set; }
    public bool Urgent { get; set; }
    public int DailyMinutes { get; set; } = 90;
    public DateOnly? OriginalStart { get; set; }
    public DateOnly? OriginalEnd { get; set; }
    public DateOnly? DeathDate { get; set; }
    public DateOnly? EventSecondDay { get; set; }
    public DateOnly? EventThirdDay { get; set; }
    public DateOnly? MedicalCertificateDate { get; set; }
    public string ObligationReference { get; set; } = "";
}
