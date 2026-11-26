namespace HRProTools.Web.Data;

public sealed class OfficialHolidayRule
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Calculation { get; set; } = "FIXED";
    public int Month { get; set; }
    public int Day { get; set; }
    public int EasterOffset { get; set; }
    public bool MayMove { get; set; }
    public int FromYear { get; set; } = 2027;
    public int? ThroughYear { get; set; }
    public string LegalBasis { get; set; } = "Ley 7544/2025, arts. 1–4";
    public string SourceUrl { get; set; } = "https://www.bacn.gov.py/leyes-paraguayas/12908/ley-n-7544-2025";
}

public sealed class LegalPermissionVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public int Revision { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string RuleJson { get; set; } = "";
    public string ChangeReason { get; set; } = "";
    public string ChangedByUserId { get; set; } = "";
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OfficialHolidayVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HolidayId { get; set; } = Guid.NewGuid();
    public int Revision { get; set; }
    public int Year { get; set; }
    public string Name { get; set; } = "";
    public DateOnly OriginalDate { get; set; }
    public DateOnly ObservedDate { get; set; }
    public string Kind { get; set; } = "NATIONAL";
    public bool IsActive { get; set; } = true;
    public string LegalBasis { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Disposition { get; set; } = "";
    public string ChangeKind { get; set; } = "INITIAL";
    public string ChangeReason { get; set; } = "";
    public string ChangedByUserId { get; set; } = "";
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OfficialCalendarYear
{
    public int Year { get; set; }
    public bool IsConfirmed { get; set; }
    public string VerificationNote { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string ChangedByUserId { get; set; } = "";
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
