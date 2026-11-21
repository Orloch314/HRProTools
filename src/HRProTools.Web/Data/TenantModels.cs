namespace HRProTools.Web.Data;

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Position
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Level { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocumentType { get; set; } = "CI";
    public string DocumentNumber { get; set; } = string.Empty;
    public string NormalizedDocumentNumber { get; set; } = string.Empty;
    public string GivenNames { get; set; } = string.Empty;
    public string FamilyNames { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class EmploymentStatuses
{
    public const string Active = "Active";
    public const string Closed = "Closed";
}

public sealed class Employment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = EmploymentStatuses.Active;
    public string ClosureReason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmploymentAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmploymentId { get; set; }
    public Employment Employment { get; set; } = null!;
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public Guid PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SalaryHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmploymentId { get; set; }
    public Employment Employment { get; set; } = null!;
    public decimal GrossSalary { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TenantBranding
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public byte[] LogoData { get; set; } = [];
    public string LogoContentType { get; set; } = string.Empty;
    public string LogoFileName { get; set; } = string.Empty;
    public string AntivirusStatus { get; set; } = string.Empty;
    public DateTimeOffset? ScannedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
    public bool Monday { get; set; } = true;
    public bool Tuesday { get; set; } = true;
    public bool Wednesday { get; set; } = true;
    public bool Thursday { get; set; } = true;
    public bool Friday { get; set; } = true;
    public bool Saturday { get; set; }
    public bool Sunday { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmploymentWorkSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmploymentId { get; set; }
    public Employment Employment { get; set; } = null!;
    public Guid WorkScheduleId { get; set; }
    public WorkSchedule WorkSchedule { get; set; } = null!;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CompanyHoliday
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class VacationPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public decimal AdditionalDaysYears1To5 { get; set; }
    public decimal AdditionalDaysYears6To10 { get; set; }
    public decimal AdditionalDaysOver10 { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class VacationPeriodOrigins
{
    public const string Generated = "GENERATED";
    public const string Imported = "IMPORTED";
}

public sealed class VacationPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmploymentId { get; set; }
    public Employment Employment { get; set; } = null!;
    public DateOnly ReferenceStart { get; set; }
    public DateOnly ReferenceEnd { get; set; }
    public DateOnly AccruedOn { get; set; }
    public DateOnly UseBy { get; set; }
    public decimal LegalDays { get; set; }
    public decimal AdditionalDays { get; set; }
    public Guid? VacationPolicyId { get; set; }
    public VacationPolicy? VacationPolicy { get; set; }
    public string Origin { get; set; } = VacationPeriodOrigins.Generated;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class VacationMovementTypes
{
    public const string Credit = "CREDIT";
    public const string Reserve = "RESERVE";
    public const string Release = "RELEASE";
    public const string Consume = "CONSUME";
    public const string Reversal = "REVERSAL";
    public const string Adjustment = "ADJUSTMENT";
}

public sealed class VacationMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VacationPeriodId { get; set; }
    public VacationPeriod VacationPeriod { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public decimal LegalDays { get; set; }
    public decimal AdditionalDays { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public Guid? SourceMovementId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class VacationRequestStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

public sealed class VacationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmploymentId { get; set; }
    public Employment Employment { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int RequestedDays { get; set; }
    public string ReservationKey { get; set; } = string.Empty;
    public string Status { get; set; } = VacationRequestStatuses.Pending;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string DecisionComment { get; set; } = string.Empty;
    public string? DecidedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public List<VacationRequestDay> Dates { get; set; } = [];
    public List<VacationRequestAllocation> Allocations { get; set; } = [];
}

public sealed class VacationRequestDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VacationRequestId { get; set; }
    public VacationRequest VacationRequest { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Guid WorkScheduleId { get; set; }
    public WorkSchedule WorkSchedule { get; set; } = null!;
    public string WorkScheduleName { get; set; } = string.Empty;
    public bool IsScheduledWorkday { get; set; }
    public bool IsHoliday { get; set; }
    public string HolidayName { get; set; } = string.Empty;
    public bool CountsAsVacationDay { get; set; }
}

public sealed class VacationRequestAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VacationRequestId { get; set; }
    public VacationRequest VacationRequest { get; set; } = null!;
    public Guid VacationPeriodId { get; set; }
    public VacationPeriod VacationPeriod { get; set; } = null!;
    public Guid ReservationMovementId { get; set; }
    public VacationMovement ReservationMovement { get; set; } = null!;
    public decimal LegalDays { get; set; }
    public decimal AdditionalDays { get; set; }
}

public sealed class TenantImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string AntivirusStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int VacationPeriodCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
