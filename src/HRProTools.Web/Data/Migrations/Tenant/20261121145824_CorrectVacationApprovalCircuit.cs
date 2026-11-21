using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class CorrectVacationApprovalCircuit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "department_approver_assignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_approver_assignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_department_approver_assignment_department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "human_resources_scope",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_human_resources_scope", x => x.Id);
                    table.ForeignKey(
                        name: "FK_human_resources_scope_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacation_approval_level",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_approval_level", x => x.Id);
                    table.CheckConstraint("ck_vacation_approval_level_sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("ck_vacation_approval_level_type", "\"Type\" IN ('APPROVER', 'HUMAN_RESOURCES')");
                });

            migrationBuilder.CreateTable(
                name: "vacation_approval_step",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VacationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_vacation_approval_step", x => x.Id);
                    table.CheckConstraint("ck_vacation_approval_step_sequence", "\"Sequence\" > 0");
                    table.CheckConstraint("ck_vacation_approval_step_status", "\"Status\" IN ('WAITING', 'PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
                    table.CheckConstraint("ck_vacation_approval_step_type", "\"Type\" IN ('APPROVER', 'HUMAN_RESOURCES')");
                    table.ForeignKey(
                        name: "FK_vacation_approval_step_vacation_request_VacationRequestId",
                        column: x => x.VacationRequestId,
                        principalTable: "vacation_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacation_workflow_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowEmployeeCancellation = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_workflow_settings", x => x.Id);
                    table.CheckConstraint("ck_vacation_workflow_settings_singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "vacation_approval_step_actor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VacationApprovalStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacation_approval_step_actor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vacation_approval_step_actor_vacation_approval_step_Vacatio~",
                        column: x => x.VacationApprovalStepId,
                        principalTable: "vacation_approval_step",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "vacation_workflow_settings",
                columns: new[] { "Id", "AllowEmployeeCancellation", "UpdatedAtUtc" },
                values: new object[] { 1, true, new DateTimeOffset(2026, 11, 21, 14, 58, 24, TimeSpan.Zero) });

            migrationBuilder.InsertData(
                table: "vacation_approval_level",
                columns: new[] { "Id", "Sequence", "Name", "Type", "IsActive" },
                values: new object[,]
                {
                    { new Guid("5e589b57-f8de-4e0c-b613-211728715ed1"), 1, "Jefe / Autorizador", "APPROVER", true },
                    { new Guid("ce9767d5-8a21-4e03-ae8a-21b7fcafae42"), 2, "Recursos Humanos", "HUMAN_RESOURCES", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_department_approver_assignment_DepartmentId_UserId",
                table: "department_approver_assignment",
                columns: new[] { "DepartmentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_human_resources_scope_CompanyId",
                table: "human_resources_scope",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_human_resources_scope_UserId_CompanyId",
                table: "human_resources_scope",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_approval_level_Sequence",
                table: "vacation_approval_level",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_approval_step_Status_Type",
                table: "vacation_approval_step",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_vacation_approval_step_VacationRequestId_Sequence",
                table: "vacation_approval_step",
                columns: new[] { "VacationRequestId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacation_approval_step_actor_UserId",
                table: "vacation_approval_step_actor",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_vacation_approval_step_actor_VacationApprovalStepId_UserId",
                table: "vacation_approval_step_actor",
                columns: new[] { "VacationApprovalStepId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "department_approver_assignment");

            migrationBuilder.DropTable(
                name: "human_resources_scope");

            migrationBuilder.DropTable(
                name: "vacation_approval_level");

            migrationBuilder.DropTable(
                name: "vacation_approval_step_actor");

            migrationBuilder.DropTable(
                name: "vacation_workflow_settings");

            migrationBuilder.DropTable(
                name: "vacation_approval_step");
        }
    }
}
