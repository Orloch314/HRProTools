using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Phase3Vacations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_holiday",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_holiday", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_holiday_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_import_batch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AntivirusStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    VacationPeriodCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_import_batch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vacation_policy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    AdditionalDaysYears1To5 = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    AdditionalDaysYears6To10 = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    AdditionalDaysOver10 = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_policy", x => x.Id);
                    table.CheckConstraint("ck_vacation_policy_days", "\"AdditionalDaysYears1To5\" >= 0 AND \"AdditionalDaysYears6To10\" >= 0 AND \"AdditionalDaysOver10\" >= 0");
                    table.ForeignKey(
                        name: "FK_vacation_policy_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_schedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BreakStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    BreakEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Monday = table.Column<bool>(type: "boolean", nullable: false),
                    Tuesday = table.Column<bool>(type: "boolean", nullable: false),
                    Wednesday = table.Column<bool>(type: "boolean", nullable: false),
                    Thursday = table.Column<bool>(type: "boolean", nullable: false),
                    Friday = table.Column<bool>(type: "boolean", nullable: false),
                    Saturday = table.Column<bool>(type: "boolean", nullable: false),
                    Sunday = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_schedule", x => x.Id);
                    table.CheckConstraint("ck_work_schedule_time", "\"EndTime\" > \"StartTime\" AND (\"BreakStart\" IS NULL OR (\"BreakEnd\" > \"BreakStart\" AND \"BreakStart\" >= \"StartTime\" AND \"BreakEnd\" <= \"EndTime\"))");
                });

            migrationBuilder.CreateTable(
                name: "vacation_period",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferenceEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    AccruedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UseBy = table.Column<DateOnly>(type: "date", nullable: false),
                    LegalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    AdditionalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    VacationPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_period", x => x.Id);
                    table.CheckConstraint("ck_vacation_period_dates", "\"ReferenceEnd\" >= \"ReferenceStart\" AND \"AccruedOn\" > \"ReferenceEnd\" AND \"UseBy\" >= \"AccruedOn\"");
                    table.CheckConstraint("ck_vacation_period_days", "\"LegalDays\" >= 0 AND \"AdditionalDays\" >= 0");
                    table.ForeignKey(
                        name: "FK_vacation_period_employment_EmploymentId",
                        column: x => x.EmploymentId,
                        principalTable: "employment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacation_period_vacation_policy_VacationPolicyId",
                        column: x => x.VacationPolicyId,
                        principalTable: "vacation_policy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employment_work_schedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employment_work_schedule", x => x.Id);
                    table.CheckConstraint("ck_employment_work_schedule_dates", "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_employment_work_schedule_employment_EmploymentId",
                        column: x => x.EmploymentId,
                        principalTable: "employment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employment_work_schedule_work_schedule_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "work_schedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacation_movement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VacationPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LegalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    AdditionalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    OperationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_movement", x => x.Id);
                    table.CheckConstraint("ck_vacation_movement_amount", "\"LegalDays\" <> 0 OR \"AdditionalDays\" <> 0");
                    table.ForeignKey(
                        name: "FK_vacation_movement_vacation_period_VacationPeriodId",
                        column: x => x.VacationPeriodId,
                        principalTable: "vacation_period",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_holiday_CompanyId_Date",
                table: "company_holiday",
                columns: new[] { "CompanyId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employment_work_schedule_EmploymentId",
                table: "employment_work_schedule",
                column: "EmploymentId",
                unique: true,
                filter: "\"ValidTo\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_employment_work_schedule_WorkScheduleId",
                table: "employment_work_schedule",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_import_batch_Sha256",
                table: "tenant_import_batch",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_vacation_movement_OperationKey_VacationPeriodId_Type",
                table: "vacation_movement",
                columns: new[] { "OperationKey", "VacationPeriodId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_movement_VacationPeriodId",
                table: "vacation_movement",
                column: "VacationPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_vacation_period_EmploymentId_ReferenceStart_ReferenceEnd",
                table: "vacation_period",
                columns: new[] { "EmploymentId", "ReferenceStart", "ReferenceEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_period_VacationPolicyId",
                table: "vacation_period",
                column: "VacationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_vacation_policy_CompanyId_EffectiveFrom",
                table: "vacation_policy",
                columns: new[] { "CompanyId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_schedule_Name",
                table: "work_schedule",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_holiday");

            migrationBuilder.DropTable(
                name: "employment_work_schedule");

            migrationBuilder.DropTable(
                name: "tenant_import_batch");

            migrationBuilder.DropTable(
                name: "vacation_movement");

            migrationBuilder.DropTable(
                name: "work_schedule");

            migrationBuilder.DropTable(
                name: "vacation_period");

            migrationBuilder.DropTable(
                name: "vacation_policy");
        }
    }
}
