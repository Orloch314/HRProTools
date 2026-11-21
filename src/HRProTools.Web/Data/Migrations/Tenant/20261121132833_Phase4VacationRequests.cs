using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Phase4VacationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vacation_request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedDays = table.Column<int>(type: "integer", nullable: false),
                    ReservationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DecidedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_request", x => x.Id);
                    table.CheckConstraint("ck_vacation_request_dates", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("ck_vacation_request_days", "\"RequestedDays\" > 0");
                    table.ForeignKey(
                        name: "FK_vacation_request_employment_EmploymentId",
                        column: x => x.EmploymentId,
                        principalTable: "employment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacation_request_allocation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VacationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    VacationPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    AdditionalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_request_allocation", x => x.Id);
                    table.CheckConstraint("ck_vacation_request_allocation_days", "\"LegalDays\" >= 0 AND \"AdditionalDays\" >= 0 AND (\"LegalDays\" <> 0 OR \"AdditionalDays\" <> 0)");
                    table.ForeignKey(
                        name: "FK_vacation_request_allocation_vacation_movement_ReservationMo~",
                        column: x => x.ReservationMovementId,
                        principalTable: "vacation_movement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacation_request_allocation_vacation_period_VacationPeriodId",
                        column: x => x.VacationPeriodId,
                        principalTable: "vacation_period",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacation_request_allocation_vacation_request_VacationReques~",
                        column: x => x.VacationRequestId,
                        principalTable: "vacation_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacation_request_day",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VacationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkScheduleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsScheduledWorkday = table.Column<bool>(type: "boolean", nullable: false),
                    IsHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    HolidayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CountsAsVacationDay = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_request_day", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vacation_request_day_vacation_request_VacationRequestId",
                        column: x => x.VacationRequestId,
                        principalTable: "vacation_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacation_request_day_work_schedule_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "work_schedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_EmploymentId_Status",
                table: "vacation_request",
                columns: new[] { "EmploymentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_ReservationKey",
                table: "vacation_request",
                column: "ReservationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_allocation_ReservationMovementId",
                table: "vacation_request_allocation",
                column: "ReservationMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_allocation_VacationPeriodId",
                table: "vacation_request_allocation",
                column: "VacationPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_allocation_VacationRequestId",
                table: "vacation_request_allocation",
                column: "VacationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_day_VacationRequestId_Date",
                table: "vacation_request_day",
                columns: new[] { "VacationRequestId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_request_day_WorkScheduleId",
                table: "vacation_request_day",
                column: "WorkScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vacation_request_allocation");

            migrationBuilder.DropTable(
                name: "vacation_request_day");

            migrationBuilder.DropTable(
                name: "vacation_request");
        }
    }
}
