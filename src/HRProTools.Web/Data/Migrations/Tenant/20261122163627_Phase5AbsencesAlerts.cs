using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Phase5AbsencesAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "absence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AffectedWorkdays = table.Column<int>(type: "integer", nullable: false),
                    DeclaredType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FinalClassification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RegisteredByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RegisteredByRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CancelledByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absence", x => x.Id);
                    table.CheckConstraint("ck_absence_dates", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("ck_absence_days", "\"AffectedWorkdays\" > 0");
                    table.CheckConstraint("ck_absence_status", "\"Status\" IN ('DECLARED', 'PENDING_REVIEW', 'JUSTIFIED', 'UNJUSTIFIED', 'CANCELLED')");
                    table.CheckConstraint("ck_absence_type", "\"DeclaredType\" IN ('MEDICAL', 'UNJUSTIFIED', 'OTHER_JUSTIFIED') AND (\"FinalClassification\" IS NULL OR \"FinalClassification\" IN ('MEDICAL', 'UNJUSTIFIED', 'OTHER_JUSTIFIED'))");
                    table.ForeignKey(
                        name: "FK_absence_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_absence_department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_absence_employment_EmploymentId",
                        column: x => x.EmploymentId,
                        principalTable: "employment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_alert",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    VacationPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttendedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AttendedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_alert", x => x.Id);
                    table.CheckConstraint("ck_operational_alert_status", "\"Status\" IN ('OPEN', 'ATTENDED')");
                });

            migrationBuilder.CreateTable(
                name: "absence_day",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkScheduleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsScheduledWorkday = table.Column<bool>(type: "boolean", nullable: false),
                    IsHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    HolidayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CountsAsAbsenceDay = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absence_day", x => x.Id);
                    table.ForeignKey(
                        name: "FK_absence_day_absence_AbsenceId",
                        column: x => x.AbsenceId,
                        principalTable: "absence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "absence_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    AntivirusStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ScannedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UploadedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absence_document", x => x.Id);
                    table.CheckConstraint("ck_absence_document_size", "\"Size\" > 0 AND \"Size\" <= 5242880");
                    table.ForeignKey(
                        name: "FK_absence_document_absence_AbsenceId",
                        column: x => x.AbsenceId,
                        principalTable: "absence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "absence_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PreviousClassification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NewClassification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absence_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_absence_history_absence_AbsenceId",
                        column: x => x.AbsenceId,
                        principalTable: "absence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_absence_CompanyId",
                table: "absence",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_absence_DepartmentId",
                table: "absence",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_absence_EmploymentId_Status",
                table: "absence",
                columns: new[] { "EmploymentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_absence_StartDate_EndDate",
                table: "absence",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_absence_day_AbsenceId_Date",
                table: "absence_day",
                columns: new[] { "AbsenceId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_absence_document_AbsenceId",
                table: "absence_document",
                column: "AbsenceId");

            migrationBuilder.CreateIndex(
                name: "IX_absence_document_Sha256",
                table: "absence_document",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_absence_history_AbsenceId_ChangedAtUtc",
                table: "absence_history",
                columns: new[] { "AbsenceId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_alert_AlertKey",
                table: "operational_alert",
                column: "AlertKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_alert_Status_CompanyId_DepartmentId",
                table: "operational_alert",
                columns: new[] { "Status", "CompanyId", "DepartmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "absence_day");

            migrationBuilder.DropTable(
                name: "absence_document");

            migrationBuilder.DropTable(
                name: "absence_history");

            migrationBuilder.DropTable(
                name: "operational_alert");

            migrationBuilder.DropTable(
                name: "absence");
        }
    }
}
