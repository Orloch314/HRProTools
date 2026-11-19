using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Global
{
    /// <inheritdoc />
    public partial class Phase3UserLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveEmploymentId",
                schema: "auth",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonId",
                schema: "auth",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId_ActiveEmploymentId",
                schema: "auth",
                table: "AspNetUsers",
                columns: new[] { "TenantId", "ActiveEmploymentId" },
                filter: "\"ActiveEmploymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId_PersonId",
                schema: "auth",
                table: "AspNetUsers",
                columns: new[] { "TenantId", "PersonId" },
                unique: true,
                filter: "\"PersonId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TenantId_ActiveEmploymentId",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TenantId_PersonId",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ActiveEmploymentId",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "auth",
                table: "AspNetUsers");
        }
    }
}
