using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class CompanyDayOffBenefit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyDayOffRequestId",
                table: "vacation_request_day",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "company_day_off_policy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_day_off_policy", x => x.Id);
                    table.CheckConstraint("ck_day_off_quantity", "\"Days\" IN (0,0.5,1)");
                    table.ForeignKey(
                        name: "FK_company_day_off_policy_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_day_off_policy_CompanyId_EffectiveFrom",
                table: "company_day_off_policy",
                columns: new[] { "CompanyId", "EffectiveFrom" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_day_off_policy");

            migrationBuilder.DropColumn(
                name: "CompanyDayOffRequestId",
                table: "vacation_request_day");
        }
    }
}
