using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Data;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonSexChange> PersonSexChanges => Set<PersonSexChange>();
    public DbSet<PermissionDocument> PermissionDocuments => Set<PermissionDocument>();
    public DbSet<PermissionLegalReview> PermissionLegalReviews => Set<PermissionLegalReview>();
    public DbSet<EmploymentNotice> EmploymentNotices => Set<EmploymentNotice>();
    public DbSet<Employment> Employments => Set<Employment>();
    public DbSet<EmploymentAssignment> EmploymentAssignments => Set<EmploymentAssignment>();
    public DbSet<SalaryHistory> SalaryHistory => Set<SalaryHistory>();
    public DbSet<TenantBranding> TenantBranding => Set<TenantBranding>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<EmploymentWorkSchedule> EmploymentWorkSchedules => Set<EmploymentWorkSchedule>();
    public DbSet<CompanyHoliday> CompanyHolidays => Set<CompanyHoliday>();
    public DbSet<VacationPolicy> VacationPolicies => Set<VacationPolicy>();
    public DbSet<CompanyDayOffPolicy> CompanyDayOffPolicies => Set<CompanyDayOffPolicy>();
    public DbSet<VacationPeriod> VacationPeriods => Set<VacationPeriod>();
    public DbSet<VacationMovement> VacationMovements => Set<VacationMovement>();
    public DbSet<VacationRequest> VacationRequests => Set<VacationRequest>();
    public DbSet<VacationRequestDay> VacationRequestDays => Set<VacationRequestDay>();
    public DbSet<VacationRequestAllocation> VacationRequestAllocations => Set<VacationRequestAllocation>();
    public DbSet<VacationWorkflowSettings> VacationWorkflowSettings => Set<VacationWorkflowSettings>();
    public DbSet<VacationApprovalLevel> VacationApprovalLevels => Set<VacationApprovalLevel>();
    public DbSet<DepartmentApproverAssignment> DepartmentApproverAssignments => Set<DepartmentApproverAssignment>();
    public DbSet<HumanResourcesScope> HumanResourcesScopes => Set<HumanResourcesScope>();
    public DbSet<VacationApprovalStep> VacationApprovalSteps => Set<VacationApprovalStep>();
    public DbSet<VacationApprovalStepActor> VacationApprovalStepActors => Set<VacationApprovalStepActor>();
    public DbSet<PermissionRequest> PermissionRequests => Set<PermissionRequest>();
    public DbSet<PermissionRequestDay> PermissionRequestDays => Set<PermissionRequestDay>();
    public DbSet<PermissionApprovalStep> PermissionApprovalSteps => Set<PermissionApprovalStep>();
    public DbSet<PermissionApprovalStepActor> PermissionApprovalStepActors => Set<PermissionApprovalStepActor>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<AbsenceDay> AbsenceDays => Set<AbsenceDay>();
    public DbSet<AbsenceHistory> AbsenceHistory => Set<AbsenceHistory>();
    public DbSet<AbsenceDocument> AbsenceDocuments => Set<AbsenceDocument>();
    public DbSet<OperationalAlert> OperationalAlerts => Set<OperationalAlert>();
    public DbSet<NotificationOutboxItem> NotificationOutbox => Set<NotificationOutboxItem>();
    public DbSet<TenantImportBatch> ImportBatches => Set<TenantImportBatch>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<CompanyDayOffPolicy>(e=>
        {
            e.ToTable("company_day_off_policy",t=>t.HasCheckConstraint("ck_day_off_quantity","\"Days\" IN (0,0.5,1)"));
            e.HasKey(x=>x.Id);
            e.HasIndex(x=>new{x.CompanyId,x.EffectiveFrom}).IsUnique();
            e.Property(x=>x.Days).HasPrecision(2,1);
            e.Property(x=>x.CreatedByUserId).HasMaxLength(450);
            e.HasOne(x=>x.Company).WithMany().HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CompanyHoliday>().Property(x => x.IsActive).HasDefaultValue(true);
        builder.Entity<CompanyHoliday>().Property(x => x.RemovedByUserId).HasMaxLength(450);
        builder.Entity<CompanyHoliday>().Property(x => x.RemovalReason).HasMaxLength(500);
        builder.Entity<PermissionDocument>(entity =>
        {
            entity.ToTable("permission_document", t => t.HasCheckConstraint("ck_permission_document_size", "\"Size\" > 0 AND \"Size\" <= 5242880"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(64);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.Property(x => x.AntivirusStatus).HasMaxLength(32);
            entity.Property(x => x.UploadedByUserId).HasMaxLength(450);
            entity.HasOne(x => x.PermissionRequest).WithMany(x => x.Documents).HasForeignKey(x => x.PermissionRequestId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PermissionLegalReview>(entity =>
        {
            entity.ToTable("permission_legal_review");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(24);
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.Property(x => x.ActorUserId).HasMaxLength(450);
            entity.HasOne(x => x.PermissionRequest).WithMany().HasForeignKey(x => x.PermissionRequestId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EmploymentNotice>(entity =>
        {
            entity.ToTable("employment_notice", t => t.HasCheckConstraint("ck_notice_dates", "\"EndDate\" >= \"StartDate\""));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasMaxLength(16);
            entity.Property(x => x.Reference).HasMaxLength(500);
            entity.Property(x => x.VerifiedByUserId).HasMaxLength(450);
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PermissionRequest>().Property(x => x.LegalCode).HasMaxLength(64);
        builder.Entity<PermissionRequest>().Property(x => x.LegalSnapshot).HasColumnType("jsonb");
        builder.Entity<PermissionRequest>().Property(x => x.LegalInput).HasColumnType("jsonb");
        builder.Entity<PermissionRequest>().Property(x => x.SexAtSubmission).HasMaxLength(2);
        builder.Entity<PermissionRequest>().Property(x => x.LegalReviewStatus).HasMaxLength(24).HasDefaultValue("NOT_REQUIRED");
        builder.Entity<PermissionRequestDay>().Property(x => x.LegalDayFraction).HasPrecision(12, 8);
        builder.Entity<Company>(entity =>
        {
            entity.ToTable("company");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32);
            entity.Property(x => x.LegalName).HasMaxLength(200);
            entity.Property(x => x.TradeName).HasMaxLength(200);
            entity.Property(x => x.TaxId).HasMaxLength(32);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Address).HasMaxLength(400);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.TaxId).IsUnique();
        });

        builder.Entity<Department>(entity =>
        {
            entity.ToTable("department");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Position>(entity =>
        {
            entity.ToTable("position");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Level).HasMaxLength(80);
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Person>(entity =>
        {
            entity.ToTable("person", t => t.HasCheckConstraint("ck_person_sex", "\"Sex\" IS NULL OR \"Sex\" IN ('M','F')"));
            entity.Property(x => x.Sex).HasMaxLength(2);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(16);
            entity.Property(x => x.DocumentNumber).HasMaxLength(40);
            entity.Property(x => x.NormalizedDocumentNumber).HasMaxLength(40);
            entity.Property(x => x.GivenNames).HasMaxLength(160);
            entity.Property(x => x.FamilyNames).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.HasIndex(x => new { x.DocumentType, x.NormalizedDocumentNumber }).IsUnique();
        });

        builder.Entity<PersonSexChange>(entity =>
        {
            entity.ToTable("person_sex_change");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PreviousValue).HasMaxLength(2);
            entity.Property(x => x.NewValue).HasMaxLength(2);
            entity.Property(x => x.ActorUserId).HasMaxLength(450);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Employment>(entity =>
        {
            entity.ToTable("employment", table =>
                table.HasCheckConstraint("ck_employment_dates", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\""));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmployeeNumber).HasMaxLength(32);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.ClosureReason).HasMaxLength(200);
            entity.HasIndex(x => x.EmployeeNumber).IsUnique();
            entity.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmploymentAssignment>(entity =>
        {
            entity.ToTable("employment_assignment", table =>
                table.HasCheckConstraint("ck_assignment_dates", "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\""));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(200);
            entity.HasIndex(x => x.EmploymentId).IsUnique().HasFilter("\"ValidTo\" IS NULL");
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalaryHistory>(entity =>
        {
            entity.ToTable("salary_history", table =>
                table.HasCheckConstraint("ck_salary_dates", "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\""));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GrossSalary).HasPrecision(18, 2);
            entity.HasIndex(x => x.EmploymentId).IsUnique().HasFilter("\"ValidTo\" IS NULL");
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TenantBranding>(entity =>
        {
            entity.ToTable("tenant_branding", table =>
                table.HasCheckConstraint("ck_tenant_branding_singleton", "\"Id\" = 1"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.LogoData).HasColumnType("bytea");
            entity.Property(x => x.LogoContentType).HasMaxLength(32);
            entity.Property(x => x.LogoFileName).HasMaxLength(255);
            entity.Property(x => x.AntivirusStatus).HasMaxLength(40);
        });

        builder.Entity<WorkSchedule>(entity =>
        {
            entity.ToTable("work_schedule", table =>
                table.HasCheckConstraint("ck_work_schedule_time", "\"EndTime\" > \"StartTime\" AND (\"BreakStart\" IS NULL OR (\"BreakEnd\" > \"BreakStart\" AND \"BreakStart\" >= \"StartTime\" AND \"BreakEnd\" <= \"EndTime\"))"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<EmploymentWorkSchedule>(entity =>
        {
            entity.ToTable("employment_work_schedule", table =>
                table.HasCheckConstraint("ck_employment_work_schedule_dates", "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\""));
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EmploymentId).IsUnique().HasFilter("\"ValidTo\" IS NULL");
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkSchedule).WithMany().HasForeignKey(x => x.WorkScheduleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CompanyHoliday>(entity =>
        {
            entity.ToTable("company_holiday");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.HasIndex(x => new { x.CompanyId, x.Date }).IsUnique();
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationPolicy>(entity =>
        {
            entity.ToTable("vacation_policy", table =>
            {
                table.HasCheckConstraint("ck_vacation_policy_days", "\"AdditionalDaysYears1To5\" >= 0 AND \"AdditionalDaysYears6To10\" >= 0 AND \"AdditionalDaysOver10\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AdditionalDaysYears1To5).HasPrecision(6, 2);
            entity.Property(x => x.AdditionalDaysYears6To10).HasPrecision(6, 2);
            entity.Property(x => x.AdditionalDaysOver10).HasPrecision(6, 2);
            entity.HasIndex(x => new { x.CompanyId, x.EffectiveFrom }).IsUnique();
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationPeriod>(entity =>
        {
            entity.ToTable("vacation_period", table =>
            {
                table.HasCheckConstraint("ck_vacation_period_dates", "\"ReferenceEnd\" >= \"ReferenceStart\" AND \"AccruedOn\" > \"ReferenceEnd\" AND \"UseBy\" >= \"AccruedOn\"");
                table.HasCheckConstraint("ck_vacation_period_days", "\"LegalDays\" >= 0 AND \"AdditionalDays\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LegalDays).HasPrecision(6, 2);
            entity.Property(x => x.AdditionalDays).HasPrecision(6, 2);
            entity.Property(x => x.Origin).HasMaxLength(16);
            entity.HasIndex(x => new { x.EmploymentId, x.ReferenceStart, x.ReferenceEnd }).IsUnique();
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.VacationPolicy).WithMany().HasForeignKey(x => x.VacationPolicyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationMovement>(entity =>
        {
            entity.ToTable("vacation_movement", table =>
                table.HasCheckConstraint("ck_vacation_movement_amount", "\"LegalDays\" <> 0 OR \"AdditionalDays\" <> 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(16);
            entity.Property(x => x.LegalDays).HasPrecision(6, 2);
            entity.Property(x => x.AdditionalDays).HasPrecision(6, 2);
            entity.Property(x => x.OperationKey).HasMaxLength(120);
            entity.Property(x => x.Note).HasMaxLength(300);
            entity.HasIndex(x => new { x.OperationKey, x.VacationPeriodId, x.Type }).IsUnique();
            entity.HasOne(x => x.VacationPeriod).WithMany().HasForeignKey(x => x.VacationPeriodId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationRequest>(entity =>
        {
            entity.ToTable("vacation_request", table =>
            {
                table.HasCheckConstraint("ck_vacation_request_dates", "\"EndDate\" >= \"StartDate\"");
                table.HasCheckConstraint("ck_vacation_request_days", "\"RequestedDays\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReservationKey).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.RequestedByUserId).HasMaxLength(450);
            entity.Property(x => x.DecisionComment).HasMaxLength(500);
            entity.Property(x => x.DecidedByUserId).HasMaxLength(450);
            entity.HasIndex(x => x.ReservationKey).IsUnique();
            entity.HasIndex(x => new { x.EmploymentId, x.Status });
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationRequestDay>(entity =>
        {
            entity.ToTable("vacation_request_day");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkScheduleName).HasMaxLength(160);
            entity.Property(x => x.HolidayName).HasMaxLength(160);
            entity.HasIndex(x => new { x.VacationRequestId, x.Date }).IsUnique();
            entity.HasOne(x => x.VacationRequest).WithMany(x => x.Dates).HasForeignKey(x => x.VacationRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkSchedule).WithMany().HasForeignKey(x => x.WorkScheduleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationRequestAllocation>(entity =>
        {
            entity.ToTable("vacation_request_allocation", table =>
                table.HasCheckConstraint("ck_vacation_request_allocation_days", "\"LegalDays\" >= 0 AND \"AdditionalDays\" >= 0 AND (\"LegalDays\" <> 0 OR \"AdditionalDays\" <> 0)"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LegalDays).HasPrecision(6, 2);
            entity.Property(x => x.AdditionalDays).HasPrecision(6, 2);
            entity.HasIndex(x => x.ReservationMovementId).IsUnique();
            entity.HasOne(x => x.VacationRequest).WithMany(x => x.Allocations).HasForeignKey(x => x.VacationRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.VacationPeriod).WithMany().HasForeignKey(x => x.VacationPeriodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReservationMovement).WithMany().HasForeignKey(x => x.ReservationMovementId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationWorkflowSettings>(entity =>
        {
            entity.ToTable("vacation_workflow_settings", table =>
                table.HasCheckConstraint("ck_vacation_workflow_settings_singleton", "\"Id\" = 1"));
            entity.HasKey(x => x.Id);
        });

        builder.Entity<VacationApprovalLevel>(entity =>
        {
            entity.ToTable("vacation_approval_level", table =>
            {
                table.HasCheckConstraint("ck_vacation_approval_level_sequence", "\"Sequence\" > 0");
                table.HasCheckConstraint("ck_vacation_approval_level_type", "\"Type\" IN ('APPROVER', 'HUMAN_RESOURCES')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Type).HasMaxLength(24);
            entity.HasIndex(x => x.Sequence).IsUnique();
        });

        builder.Entity<DepartmentApproverAssignment>(entity =>
        {
            entity.ToTable("department_approver_assignment");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.DepartmentId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HumanResourcesScope>(entity =>
        {
            entity.ToTable("human_resources_scope");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique();
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationApprovalStep>(entity =>
        {
            entity.ToTable("vacation_approval_step", table =>
            {
                table.HasCheckConstraint("ck_vacation_approval_step_sequence", "\"Sequence\" > 0");
                table.HasCheckConstraint("ck_vacation_approval_step_type", "\"Type\" IN ('APPROVER', 'HUMAN_RESOURCES')");
                table.HasCheckConstraint("ck_vacation_approval_step_status", "\"Status\" IN ('WAITING', 'PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Type).HasMaxLength(24);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.DecidedByUserId).HasMaxLength(450);
            entity.Property(x => x.DecisionComment).HasMaxLength(500);
            entity.HasIndex(x => new { x.VacationRequestId, x.Sequence }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.Type });
            entity.HasOne(x => x.VacationRequest).WithMany(x => x.ApprovalSteps).HasForeignKey(x => x.VacationRequestId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VacationApprovalStepActor>(entity =>
        {
            entity.ToTable("vacation_approval_step_actor");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.VacationApprovalStepId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.VacationApprovalStep).WithMany(x => x.Actors).HasForeignKey(x => x.VacationApprovalStepId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PermissionRequest>(entity =>
        {
            entity.ToTable("permission_request", table =>
            {
                table.HasCheckConstraint("ck_permission_request_dates", "\"EndDate\" >= \"StartDate\"");
                table.HasCheckConstraint("ck_permission_request_type", "\"PermissionType\" IN ('PAID', 'UNPAID')");
                table.HasCheckConstraint("ck_permission_request_unit", "\"Unit\" IN ('DAYS', 'HOURS')");
                table.HasCheckConstraint("ck_permission_request_quantity", "(\"Unit\" = 'DAYS' AND \"RequestedDays\" > 0 AND \"RequestedMinutes\" = 0 AND \"StartTime\" IS NULL AND \"EndTime\" IS NULL) OR (\"Unit\" = 'HOURS' AND \"RequestedDays\" = 0 AND \"RequestedMinutes\" > 0 AND \"StartTime\" IS NOT NULL AND \"EndTime\" IS NOT NULL AND (\"StartDate\" = \"EndDate\" OR \"LegalCode\" IN ('LACTANCIA','BUSQUEDA_EMPLEO')))");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PermissionType).HasMaxLength(12);
            entity.Property(x => x.Unit).HasMaxLength(12);
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.RequestedByUserId).HasMaxLength(450);
            entity.Property(x => x.DecisionComment).HasMaxLength(500);
            entity.Property(x => x.DecidedByUserId).HasMaxLength(450);
            entity.Property(x => x.CancelledByUserId).HasMaxLength(450);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.EmploymentId, x.Status });
            entity.HasIndex(x => new { x.StartDate, x.EndDate });
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PermissionRequestDay>(entity =>
        {
            entity.ToTable("permission_request_day", table =>
                table.HasCheckConstraint("ck_permission_request_day_minutes", "\"EffectiveMinutes\" >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkScheduleName).HasMaxLength(160);
            entity.Property(x => x.HolidayName).HasMaxLength(160);
            entity.HasIndex(x => new { x.PermissionRequestId, x.Date }).IsUnique();
            entity.HasOne(x => x.PermissionRequest).WithMany(x => x.Dates).HasForeignKey(x => x.PermissionRequestId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PermissionApprovalStep>(entity =>
        {
            entity.ToTable("permission_approval_step", table =>
            {
                table.HasCheckConstraint("ck_permission_approval_step_sequence", "\"Sequence\" > 0");
                table.HasCheckConstraint("ck_permission_approval_step_type", "\"Type\" IN ('APPROVER', 'HUMAN_RESOURCES')");
                table.HasCheckConstraint("ck_permission_approval_step_status", "\"Status\" IN ('WAITING', 'PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Type).HasMaxLength(24);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.DecidedByUserId).HasMaxLength(450);
            entity.Property(x => x.DecisionComment).HasMaxLength(500);
            entity.HasIndex(x => new { x.PermissionRequestId, x.Sequence }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.Type });
            entity.HasOne(x => x.PermissionRequest).WithMany(x => x.ApprovalSteps).HasForeignKey(x => x.PermissionRequestId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PermissionApprovalStepActor>(entity =>
        {
            entity.ToTable("permission_approval_step_actor");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.PermissionApprovalStepId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.PermissionApprovalStep).WithMany(x => x.Actors).HasForeignKey(x => x.PermissionApprovalStepId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Absence>(entity =>
        {
            entity.ToTable("absence", table =>
            {
                table.HasCheckConstraint("ck_absence_dates", "\"EndDate\" >= \"StartDate\"");
                table.HasCheckConstraint("ck_absence_days", "\"AffectedWorkdays\" > 0");
                table.HasCheckConstraint("ck_absence_type", "\"DeclaredType\" IN ('MEDICAL', 'UNJUSTIFIED', 'OTHER_JUSTIFIED') AND (\"FinalClassification\" IS NULL OR \"FinalClassification\" IN ('MEDICAL', 'UNJUSTIFIED', 'OTHER_JUSTIFIED'))");
                table.HasCheckConstraint("ck_absence_status", "\"Status\" IN ('DECLARED', 'PENDING_REVIEW', 'JUSTIFIED', 'UNJUSTIFIED', 'CANCELLED')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeclaredType).HasMaxLength(32);
            entity.Property(x => x.FinalClassification).HasMaxLength(32);
            entity.Property(x => x.Status).HasMaxLength(24);
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.RegisteredByUserId).HasMaxLength(450);
            entity.Property(x => x.RegisteredByRole).HasMaxLength(32);
            entity.Property(x => x.ResolvedByUserId).HasMaxLength(450);
            entity.Property(x => x.ResolutionComment).HasMaxLength(1000);
            entity.Property(x => x.CancelledByUserId).HasMaxLength(450);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.EmploymentId, x.Status });
            entity.HasIndex(x => new { x.StartDate, x.EndDate });
            entity.HasOne(x => x.Employment).WithMany().HasForeignKey(x => x.EmploymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AbsenceDay>(entity =>
        {
            entity.ToTable("absence_day");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkScheduleName).HasMaxLength(160);
            entity.Property(x => x.HolidayName).HasMaxLength(160);
            entity.HasIndex(x => new { x.AbsenceId, x.Date }).IsUnique();
            entity.HasOne(x => x.Absence).WithMany(x => x.Dates).HasForeignKey(x => x.AbsenceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AbsenceHistory>(entity =>
        {
            entity.ToTable("absence_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PreviousStatus).HasMaxLength(24);
            entity.Property(x => x.NewStatus).HasMaxLength(24);
            entity.Property(x => x.PreviousClassification).HasMaxLength(32);
            entity.Property(x => x.NewClassification).HasMaxLength(32);
            entity.Property(x => x.ChangedByUserId).HasMaxLength(450);
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.HasIndex(x => new { x.AbsenceId, x.ChangedAtUtc });
            entity.HasOne(x => x.Absence).WithMany(x => x.History).HasForeignKey(x => x.AbsenceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AbsenceDocument>(entity =>
        {
            entity.ToTable("absence_document", table => table.HasCheckConstraint("ck_absence_document_size", "\"Size\" > 0 AND \"Size\" <= 5242880"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(64);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.Property(x => x.Content).HasColumnType("bytea");
            entity.Property(x => x.AntivirusStatus).HasMaxLength(40);
            entity.Property(x => x.UploadedByUserId).HasMaxLength(450);
            entity.HasIndex(x => x.Sha256);
            entity.HasOne(x => x.Absence).WithMany(x => x.Documents).HasForeignKey(x => x.AbsenceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OperationalAlert>(entity =>
        {
            entity.ToTable("operational_alert", table => table.HasCheckConstraint("ck_operational_alert_status", "\"Status\" IN ('OPEN', 'ATTENDED')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AlertKey).HasMaxLength(180);
            entity.Property(x => x.Type).HasMaxLength(48);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Message).HasMaxLength(500);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.AttendedByUserId).HasMaxLength(450);
            entity.HasIndex(x => x.AlertKey).IsUnique();
            entity.HasIndex(x => new { x.Status, x.CompanyId, x.DepartmentId });
        });

        builder.Entity<NotificationOutboxItem>(entity =>
        {
            entity.ToTable("notification_outbox", table =>
            {
                table.HasCheckConstraint("ck_notification_outbox_status", "\"Status\" IN ('PENDING', 'SENT', 'FAILED')");
                table.HasCheckConstraint("ck_notification_outbox_attempts", "\"AttemptCount\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventKey).HasMaxLength(200);
            entity.Property(x => x.EventType).HasMaxLength(40);
            entity.Property(x => x.RequestType).HasMaxLength(20);
            entity.Property(x => x.RecipientUserId).HasMaxLength(450);
            entity.Property(x => x.Subject).HasMaxLength(255);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.HasIndex(x => x.EventKey).IsUnique();
            entity.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        });

        builder.Entity<TenantImportBatch>(entity =>
        {
            entity.ToTable("tenant_import_batch");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(255);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.Property(x => x.AntivirusStatus).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(24);
            entity.HasIndex(x => x.Sha256);
        });
    }
}
