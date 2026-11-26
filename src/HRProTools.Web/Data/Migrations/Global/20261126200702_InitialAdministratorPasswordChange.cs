using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Global
{
    /// <inheritdoc />
    public partial class InitialAdministratorPasswordChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "auth",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "auth",
                table: "AspNetUsers");
        }
    }
}
