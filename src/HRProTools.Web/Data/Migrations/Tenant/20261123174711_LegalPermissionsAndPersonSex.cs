using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class LegalPermissionsAndPersonSex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_permission_request_quantity",
                table: "permission_request");

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "person",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "ND");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "IntervalEnd",
                table: "permission_request_day",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "IntervalStart",
                table: "permission_request_day",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalDayFraction",
                table: "permission_request_day",
                type: "numeric(12,8)",
                precision: 12,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LegalCode",
                table: "permission_request",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalInput",
                table: "permission_request",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalReviewStatus",
                table: "permission_request",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "NOT_REQUIRED");

            migrationBuilder.AddColumn<string>(
                name: "LegalSnapshot",
                table: "permission_request",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SexAtSubmission",
                table: "permission_request",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employment_notice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VerifiedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employment_notice", x => x.Id);
                    table.CheckConstraint("ck_notice_dates", "\"EndDate\" >= \"StartDate\"");
                    table.ForeignKey(
                        name: "FK_employment_notice_employment_EmploymentId",
                        column: x => x.EmploymentId,
                        principalTable: "employment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AntivirusStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_document", x => x.Id);
                    table.CheckConstraint("ck_permission_document_size", "\"Size\" > 0 AND \"Size\" <= 5242880");
                    table.ForeignKey(
                        name: "FK_permission_document_permission_request_PermissionRequestId",
                        column: x => x.PermissionRequestId,
                        principalTable: "permission_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_legal_review",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DocumentDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_legal_review", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_legal_review_permission_request_PermissionReques~",
                        column: x => x.PermissionRequestId,
                        principalTable: "permission_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_sex_change",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousValue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    NewValue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_sex_change", x => x.Id);
                    table.ForeignKey(
                        name: "FK_person_sex_change_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_person_sex",
                table: "person",
                sql: "\"Sex\" IN ('M','F','ND')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_permission_request_quantity",
                table: "permission_request",
                sql: "(\"Unit\" = 'DAYS' AND \"RequestedDays\" > 0 AND \"RequestedMinutes\" = 0 AND \"StartTime\" IS NULL AND \"EndTime\" IS NULL) OR (\"Unit\" = 'HOURS' AND \"RequestedDays\" = 0 AND \"RequestedMinutes\" > 0 AND \"StartTime\" IS NOT NULL AND \"EndTime\" IS NOT NULL AND (\"StartDate\" = \"EndDate\" OR \"LegalCode\" IN ('LACTANCIA','BUSQUEDA_EMPLEO')))");

            migrationBuilder.CreateIndex(
                name: "IX_employment_notice_EmploymentId",
                table: "employment_notice",
                column: "EmploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_permission_document_PermissionRequestId",
                table: "permission_document",
                column: "PermissionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_permission_legal_review_PermissionRequestId",
                table: "permission_legal_review",
                column: "PermissionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_person_sex_change_PersonId",
                table: "person_sex_change",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employment_notice");

            migrationBuilder.DropTable(
                name: "permission_document");

            migrationBuilder.DropTable(
                name: "permission_legal_review");

            migrationBuilder.DropTable(
                name: "person_sex_change");

            migrationBuilder.DropCheckConstraint(
                name: "ck_person_sex",
                table: "person");

            migrationBuilder.DropCheckConstraint(
                name: "ck_permission_request_quantity",
                table: "permission_request");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "person");

            migrationBuilder.DropColumn(
                name: "IntervalEnd",
                table: "permission_request_day");

            migrationBuilder.DropColumn(
                name: "IntervalStart",
                table: "permission_request_day");

            migrationBuilder.DropColumn(
                name: "LegalDayFraction",
                table: "permission_request_day");

            migrationBuilder.DropColumn(
                name: "LegalCode",
                table: "permission_request");

            migrationBuilder.DropColumn(
                name: "LegalInput",
                table: "permission_request");

            migrationBuilder.DropColumn(
                name: "LegalReviewStatus",
                table: "permission_request");

            migrationBuilder.DropColumn(
                name: "LegalSnapshot",
                table: "permission_request");

            migrationBuilder.DropColumn(
                name: "SexAtSubmission",
                table: "permission_request");

            migrationBuilder.AddCheckConstraint(
                name: "ck_permission_request_quantity",
                table: "permission_request",
                sql: "(\"Unit\" = 'DAYS' AND \"RequestedDays\" > 0 AND \"RequestedMinutes\" = 0 AND \"StartTime\" IS NULL AND \"EndTime\" IS NULL) OR (\"Unit\" = 'HOURS' AND \"RequestedDays\" = 0 AND \"RequestedMinutes\" > 0 AND \"StartTime\" IS NOT NULL AND \"EndTime\" IS NOT NULL AND \"StartDate\" = \"EndDate\")");
        }
    }
}
