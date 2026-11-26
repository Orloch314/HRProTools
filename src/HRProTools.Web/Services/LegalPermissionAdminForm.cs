namespace HRProTools.Web.Services;

public sealed class LegalPermissionAdminForm
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Group { get; set; } = "Permisos legales";
    public string Description { get; set; } = "";
    public string Law { get; set; } = "";
    public string Article { get; set; } = "";
    public string Source { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Unit { get; set; } = "DAYS";
    public decimal? Maximum { get; set; }
    public decimal? Event { get; set; }
    public decimal? Month { get; set; }
    public decimal? Annual { get; set; }
    public string Beneficiaries { get; set; } = "Funcionarios que cumplan los requisitos";
    public string Requirements { get; set; } = "";
    public string Documentation { get; set; } = "";
    public string Notice { get; set; } = "Según norma aplicable";
    public string Remuneration { get; set; } = "";
    public bool Paid { get; set; } = true;
    public bool Protected { get; set; } = true;
    public bool Active { get; set; } = true;
    public DateOnly From { get; set; } = GlobalReferenceQueries.Today;
    public DateOnly? To { get; set; }
    public string Reason { get; set; } = "";
    public LegalPermissionType? Previous { get; set; }
    public LegalPermissionType Rule() => new(Code, Name, Group, Law, Article, Duration, Unit, Beneficiaries, Requirements, Documentation, Notice, Remuneration, Protected, Previous?.Medical ?? false)
    { Description = Description, SourceUrl = Source, Engine = Previous?.Engine ?? "PARAMETRIC", QuantityUnit = Previous?.Engine == "SPECIAL" ? Previous.QuantityUnit : Unit == "DAYS_OR_HOURS" ? "DAYS" : Unit, MaximumQuantity = Maximum, EventLimit = Event, MonthlyLimit = Month, AnnualLimit = Annual, IsPaid = Paid };
    public static LegalPermissionAdminForm FromRule(LegalPermissionType rule, bool active, DateOnly from) => new()
    { Previous = rule, Code = rule.Code, Name = rule.Name, Group = rule.Group, Description = rule.Description, Law = rule.Law, Article = rule.Article, Source = rule.SourceUrl, Duration = rule.Duration, Unit = rule.Unit, Maximum = rule.MaximumQuantity, Event = rule.EventLimit, Month = rule.MonthlyLimit, Annual = rule.AnnualLimit, Beneficiaries = rule.Beneficiaries, Requirements = rule.Requirements, Documentation = rule.Documentation, Notice = rule.Notice, Remuneration = rule.Remuneration, Paid = rule.IsPaid, Protected = rule.Protected, Active = active, From = from };
}
