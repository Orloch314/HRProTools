using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Data;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Employment> Employments => Set<Employment>();
    public DbSet<EmploymentAssignment> EmploymentAssignments => Set<EmploymentAssignment>();
    public DbSet<SalaryHistory> SalaryHistory => Set<SalaryHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
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
            entity.ToTable("person");
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
    }
}
