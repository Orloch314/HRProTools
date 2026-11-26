using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SmtpConfiguration> SmtpConfigurations => Set<SmtpConfiguration>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();
    public DbSet<LegalPermissionVersion> LegalPermissionVersions => Set<LegalPermissionVersion>();
    public DbSet<OfficialHolidayVersion> OfficialHolidayVersions => Set<OfficialHolidayVersion>();
    public DbSet<OfficialCalendarYear> OfficialCalendarYears => Set<OfficialCalendarYear>();
    public DbSet<OfficialHolidayRule> OfficialHolidayRules => Set<OfficialHolidayRule>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("auth");
        builder.Entity<OfficialHolidayRule>(e =>
        {
            e.ToTable("official_holiday_rule", "platform");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Calculation).HasMaxLength(24);
            e.Property(x => x.LegalBasis).HasMaxLength(500);
            e.Property(x => x.SourceUrl).HasMaxLength(1500);
        });
        builder.Entity<LegalPermissionVersion>(e =>
        {
            e.ToTable("legal_permission_version", "platform", t => t.HasCheckConstraint("ck_legal_version_dates", "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\""));
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.RuleJson).HasColumnType("jsonb");
            e.Property(x => x.ChangeReason).HasMaxLength(1000);
            e.Property(x => x.ChangedByUserId).HasMaxLength(450);
            e.HasIndex(x => new { x.Code, x.Revision }).IsUnique();
        });
        builder.Entity<OfficialHolidayVersion>(e =>
        {
            e.ToTable("official_holiday_version", "platform");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.HolidayId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Kind).HasMaxLength(24);
            e.Property(x => x.ChangeKind).HasMaxLength(24);
            e.Property(x => x.LegalBasis).HasMaxLength(500);
            e.Property(x => x.SourceUrl).HasMaxLength(1500);
            e.Property(x => x.Disposition).HasMaxLength(500);
            e.Property(x => x.ChangeReason).HasMaxLength(1000);
            e.Property(x => x.ChangedByUserId).HasMaxLength(450);
        });
        builder.Entity<OfficialCalendarYear>(e =>
        {
            e.ToTable("official_calendar_year", "platform");
            e.HasKey(x => x.Year);
            e.Property(x => x.VerificationNote).HasMaxLength(1000);
            e.Property(x => x.SourceUrl).HasMaxLength(1500);
            e.Property(x => x.ChangedByUserId).HasMaxLength(450);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(160);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.PersonId }).IsUnique().HasFilter("\"PersonId\" IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.ActiveEmploymentId }).HasFilter("\"ActiveEmploymentId\" IS NOT NULL");
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenant", "platform");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.SchemaName).HasMaxLength(63);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.ProvisioningError).HasMaxLength(1000);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.SchemaName).IsUnique();
        });

        builder.Entity<SmtpConfiguration>(entity =>
        {
            entity.ToTable("smtp_configuration", "platform", table =>
                table.HasCheckConstraint("ck_smtp_singleton", "\"Id\" = 1"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Host).HasMaxLength(255);
            entity.Property(x => x.SecurityMode).HasMaxLength(32);
            entity.Property(x => x.Username).HasMaxLength(255);
            entity.Property(x => x.ProtectedPassword).HasMaxLength(4000);
            entity.Property(x => x.FromAddress).HasMaxLength(320);
            entity.Property(x => x.FromName).HasMaxLength(160);
            entity.Property(x => x.UpdatedBy).HasMaxLength(320);
        });

        builder.Entity<OperationLog>(entity =>
        {
            entity.ToTable("operation_log", "platform");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorUserId).HasMaxLength(450);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.Subject).HasMaxLength(300);
            entity.Property(x => x.Detail).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
        });
    }
}
