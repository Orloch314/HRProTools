using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SmtpConfiguration> SmtpConfigurations => Set<SmtpConfiguration>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("auth");

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
