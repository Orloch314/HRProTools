using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20261118174500_TenantBranding")]
public partial class TenantBrandingMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_branding",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                LogoData = table.Column<byte[]>(type: "bytea", nullable: false),
                LogoContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                LogoFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                AntivirusStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ScannedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_branding", x => x.Id);
                table.CheckConstraint("ck_tenant_branding_singleton", "\"Id\" = 1");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tenant_branding");
    }
}
