using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class CompanyHolidayLogicalRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "company_holiday",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovalReason",
                table: "company_holiday",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAtUtc",
                table: "company_holiday",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovedByUserId",
                table: "company_holiday",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "company_holiday");

            migrationBuilder.DropColumn(
                name: "RemovalReason",
                table: "company_holiday");

            migrationBuilder.DropColumn(
                name: "RemovedAtUtc",
                table: "company_holiday");

            migrationBuilder.DropColumn(
                name: "RemovedByUserId",
                table: "company_holiday");
        }
    }
}
