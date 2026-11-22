using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Phase4PermissionsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_outbox", x => x.Id);
                    table.CheckConstraint("ck_notification_outbox_attempts", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("ck_notification_outbox_status", "\"Status\" IN ('PENDING', 'SENT', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "permission_request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionType = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Unit = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    RequestedDays = table.Column<int>(type: "integer", nullable: false),
                    RequestedMinutes = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DecidedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_request", x => x.Id);
                    table.CheckConstraint("ck_permission_request_dates", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("ck_permission_request_quantity", "(\"Unit\" = 'DAYS' AND \"RequestedDays\" > 0 AND \"RequestedMinutes\" = 0 AND \"StartTime\" IS NULL AND \"EndTime\" IS NULL) OR (\"Unit\" = 'HOURS' AND \"RequestedDays\" = 0 AND \"RequestedMinutes\" > 0 AND \"StartTime\" IS NOT NULL AND \"EndTime\" IS NOT NULL AND \"StartDate\" = \"EndDate\")");
                    table.CheckConstraint("ck_permission_request_type", "\"PermissionType\" IN ('PAID', 'UNPAID')");
                    table.CheckConstraint("ck_permission_request_unit", "\"Unit\" IN ('DAYS', 'HOURS')");
                    table.ForeignKey(
                        name: "FK_permission_request_employment_EmploymentId",
                        column: x => x.EmploymentId,
                        principalTable: "employment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_approval_step",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_approval_step", x => x.Id);
                    table.CheckConstraint("ck_permission_approval_step_sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("ck_permission_approval_step_status", "\"Status\" IN ('WAITING', 'PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
                    table.CheckConstraint("ck_permission_approval_step_type", "\"Type\" IN ('APPROVER', 'HUMAN_RESOURCES')");
                    table.ForeignKey(
                        name: "FK_permission_approval_step_permission_request_PermissionReque~",
                        column: x => x.PermissionRequestId,
                        principalTable: "permission_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_request_day",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkScheduleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WorkStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    WorkEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BreakStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    BreakEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsScheduledWorkday = table.Column<bool>(type: "boolean", nullable: false),
                    IsHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    HolidayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CountsAsPermissionDay = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_request_day", x => x.Id);
                    table.CheckConstraint("ck_permission_request_day_minutes", "\"EffectiveMinutes\" >= 0");
                    table.ForeignKey(
                        name: "FK_permission_request_day_permission_request_PermissionRequest~",
                        column: x => x.PermissionRequestId,
                        principalTable: "permission_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_approval_step_actor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionApprovalStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_approval_step_actor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_approval_step_actor_permission_approval_step_Per~",
                        column: x => x.PermissionApprovalStepId,
                        principalTable: "permission_approval_step",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_EventKey",
                table: "notification_outbox",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_Status_NextAttemptAtUtc",
                table: "notification_outbox",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_permission_approval_step_PermissionRequestId_Sequence",
                table: "permission_approval_step",
                columns: new[] { "PermissionRequestId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_approval_step_Status_Type",
                table: "permission_approval_step",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_permission_approval_step_actor_PermissionApprovalStepId_Use~",
                table: "permission_approval_step_actor",
                columns: new[] { "PermissionApprovalStepId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_approval_step_actor_UserId",
                table: "permission_approval_step_actor",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_permission_request_EmploymentId_Status",
                table: "permission_request",
                columns: new[] { "EmploymentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_permission_request_StartDate_EndDate",
                table: "permission_request",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_permission_request_day_PermissionRequestId_Date",
                table: "permission_request_day",
                columns: new[] { "PermissionRequestId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_outbox");

            migrationBuilder.DropTable(
                name: "permission_approval_step_actor");

            migrationBuilder.DropTable(
                name: "permission_request_day");

            migrationBuilder.DropTable(
                name: "permission_approval_step");

            migrationBuilder.DropTable(
                name: "permission_request");
        }
    }
}
