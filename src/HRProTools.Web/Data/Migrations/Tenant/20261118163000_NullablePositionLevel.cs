using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant;

[DbContext(typeof(TenantDbContext))]
[Migration("20261118163000_NullablePositionLevel")]
public partial class NullablePositionLevel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Level",
            table: "position",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(80)",
            oldMaxLength: 80);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE position SET \"Level\" = '' WHERE \"Level\" IS NULL");
        migrationBuilder.AlterColumn<string>(
            name: "Level",
            table: "position",
            type: "character varying(80)",
            maxLength: 80,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(80)",
            oldMaxLength: 80,
            oldNullable: true);
    }
}
