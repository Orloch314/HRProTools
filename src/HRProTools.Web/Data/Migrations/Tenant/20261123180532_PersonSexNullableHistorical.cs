using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PersonSexNullableHistorical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_person_sex",
                table: "person");

            migrationBuilder.AlterColumn<string>(
                name: "PreviousValue",
                table: "person_sex_change",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Sex",
                table: "person",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2,
                oldDefaultValue: "ND");

            // Only the sentinel introduced by the preceding migration is converted.
            // M/F and all other person data remain untouched; historical request/audit snapshots are preserved.
            migrationBuilder.Sql("UPDATE person SET \"Sex\" = NULL WHERE \"Sex\" = 'ND';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_person_sex",
                table: "person",
                sql: "\"Sex\" IS NULL OR \"Sex\" IN ('M','F')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException("La corrección de Sexo no se revierte automáticamente: no se reinventa un valor para los datos históricos faltantes.");
        }
    }
}
