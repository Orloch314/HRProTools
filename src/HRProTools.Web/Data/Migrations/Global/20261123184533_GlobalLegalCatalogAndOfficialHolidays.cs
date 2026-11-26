using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Global
{
    /// <inheritdoc />
    public partial class GlobalLegalCatalogAndOfficialHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_permission_version",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RuleJson = table.Column<string>(type: "jsonb", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_permission_version", x => x.Id);
                    table.CheckConstraint("ck_legal_version_dates", "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"");
                });

            migrationBuilder.CreateTable(
                name: "official_calendar_year",
                schema: "platform",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_calendar_year", x => x.Year);
                });

            migrationBuilder.CreateTable(
                name: "official_holiday_version",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HolidayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ObservedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    Disposition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangeKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_holiday_version", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legal_permission_version_Code_Revision",
                schema: "platform",
                table: "legal_permission_version",
                columns: new[] { "Code", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_official_holiday_version_HolidayId_Revision",
                schema: "platform",
                table: "official_holiday_version",
                columns: new[] { "HolidayId", "Revision" },
                unique: true);
            HRProTools.Web.Services.InitialGlobalReferenceSeed.Insert(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_permission_version",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "official_calendar_year",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "official_holiday_version",
                schema: "platform");
        }
    }
}
