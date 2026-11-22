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

public static class VacationApprovalLevelTypes
{
    public const string Approver = "APPROVER";
    public const string HumanResources = "HUMAN_RESOURCES";
}

public static class VacationApprovalStepStatuses
{
    public const string Waiting = "WAITING";
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

public sealed class VacationWorkflowSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public bool AllowEmployeeCancellation { get; set; } = true;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class VacationApprovalLevel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class DepartmentApproverAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
}

public sealed class HumanResourcesScope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
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
    public List<VacationApprovalStep> ApprovalSteps { get; set; } = [];
}

public sealed class VacationApprovalStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VacationRequestId { get; set; }
    public VacationRequest VacationRequest { get; set; } = null!;
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = VacationApprovalStepStatuses.Waiting;
    public Guid DepartmentId { get; set; }
    public Guid CompanyId { get; set; }
    public string? DecidedByUserId { get; set; }
    public string DecisionComment { get; set; } = string.Empty;
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public List<VacationApprovalStepActor> Actors { get; set; } = [];
}

public sealed class VacationApprovalStepActor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VacationApprovalStepId { get; set; }
    public VacationApprovalStep VacationApprovalStep { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
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

public static class PermissionRequestTypes
{
    public const string Paid = "PAID";
    public const string Unpaid = "UNPAID";
}

public static class PermissionRequestUnits
{
    public const string Days = "DAYS";
    public const string Hours = "HOURS";
}

public static class PermissionRequestStatuses
{
    public const string Pending = VacationRequestStatuses.Pending;
    public const string Approved = VacationRequestStatuses.Approved;
    public const string Rejected = VacationRequestStatuses.Rejected;
    public const string Cancelled = VacationRequestStatuses.Cancelled;
}

public sealed class PermissionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmploymentId { get; set; }
    public Employment Employment { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid DepartmentId { get; set; }
    public string PermissionType { get; set; } = PermissionRequestTypes.Paid;
    public string Unit { get; set; } = PermissionRequestUnits.Days;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int RequestedDays { get; set; }
    public int RequestedMinutes { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = PermissionRequestStatuses.Pending;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string DecisionComment { get; set; } = string.Empty;
    public string? DecidedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? CancelledByUserId { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public List<PermissionRequestDay> Dates { get; set; } = [];
    public List<PermissionApprovalStep> ApprovalSteps { get; set; } = [];
}

public sealed class PermissionRequestDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PermissionRequestId { get; set; }
    public PermissionRequest PermissionRequest { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Guid WorkScheduleId { get; set; }
    public string WorkScheduleName { get; set; } = string.Empty;
    public TimeOnly WorkStart { get; set; }
    public TimeOnly WorkEnd { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
    public bool IsScheduledWorkday { get; set; }
    public bool IsHoliday { get; set; }
    public string HolidayName { get; set; } = string.Empty;
    public bool CountsAsPermissionDay { get; set; }
    public int EffectiveMinutes { get; set; }
}

public sealed class PermissionApprovalStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PermissionRequestId { get; set; }
    public PermissionRequest PermissionRequest { get; set; } = null!;
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = VacationApprovalStepStatuses.Waiting;
    public Guid DepartmentId { get; set; }
    public Guid CompanyId { get; set; }
    public string? DecidedByUserId { get; set; }
    public string DecisionComment { get; set; } = string.Empty;
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public List<PermissionApprovalStepActor> Actors { get; set; } = [];
}

public sealed class PermissionApprovalStepActor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PermissionApprovalStepId { get; set; }
    public PermissionApprovalStep PermissionApprovalStep { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
}

public static class AbsenceTypes
{
    public const string Medical = "MEDICAL";
    public const string Unjustified = "UNJUSTIFIED";
    public const string OtherJustified = "OTHER_JUSTIFIED";
}

public static class AbsenceStatuses
{
    public const string Declared = "DECLARED";
    public const string PendingReview = "PENDING_REVIEW";
    public const string Justified = "JUSTIFIED";
    public const string Unjustified = "UNJUSTIFIED";
    public const string Cancelled = "CANCELLED";
}

public sealed class Absence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmploymentId { get; set; }
    public Employment Employment { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int AffectedWorkdays { get; set; }
    public string DeclaredType { get; set; } = AbsenceTypes.OtherJustified;
    public string? FinalClassification { get; set; }
    public string Status { get; set; } = AbsenceStatuses.Declared;
    public string Reason { get; set; } = string.Empty;
    public string RegisteredByUserId { get; set; } = string.Empty;
    public string RegisteredByRole { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public string ResolutionComment { get; set; } = string.Empty;
    public string? CancelledByUserId { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
    public List<AbsenceDay> Dates { get; set; } = [];
    public List<AbsenceHistory> History { get; set; } = [];
    public List<AbsenceDocument> Documents { get; set; } = [];
}

public sealed class AbsenceDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AbsenceId { get; set; }
    public Absence Absence { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Guid WorkScheduleId { get; set; }
    public string WorkScheduleName { get; set; } = string.Empty;
    public bool IsScheduledWorkday { get; set; }
    public bool IsHoliday { get; set; }
    public string HolidayName { get; set; } = string.Empty;
    public bool CountsAsAbsenceDay { get; set; }
}

public sealed class AbsenceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AbsenceId { get; set; }
    public Absence Absence { get; set; } = null!;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? PreviousClassification { get; set; }
    public string? NewClassification { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AbsenceDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AbsenceId { get; set; }
    public Absence Absence { get; set; } = null!;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public string AntivirusStatus { get; set; } = string.Empty;
    public DateTimeOffset? ScannedAtUtc { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class OperationalAlertTypes
{
    public const string VacationSecondPendingRisk = "VACATION_SECOND_PENDING_RISK";
    public const string AbsencePendingReview = "ABSENCE_PENDING_REVIEW";
    public const string AbsenceUnjustified = "ABSENCE_UNJUSTIFIED";
    public const string MedicalDocumentPending = "MEDICAL_DOCUMENT_PENDING";
}

public static class OperationalAlertStatuses
{
    public const string Open = "OPEN";
    public const string Attended = "ATTENDED";
}

public sealed class OperationalAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AlertKey { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid EmploymentId { get; set; }
    public Guid? AbsenceId { get; set; }
    public Guid? VacationPeriodId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = OperationalAlertStatuses.Open;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? AttendedByUserId { get; set; }
    public DateTimeOffset? AttendedAtUtc { get; set; }
}

public static class NotificationOutboxStatuses
{
    public const string Pending = "PENDING";
    public const string Sent = "SENT";
    public const string Failed = "FAILED";
}

public sealed class NotificationOutboxItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string Status { get; set; } = NotificationOutboxStatuses.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string LastError { get; set; } = string.Empty;
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
