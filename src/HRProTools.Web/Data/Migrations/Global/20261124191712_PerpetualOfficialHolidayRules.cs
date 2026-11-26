using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRProTools.Web.Data.Migrations.Global
{
    /// <inheritdoc />
    public partial class PerpetualOfficialHolidayRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "official_holiday_rule",
                schema: "platform",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Calculation = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    EasterOffset = table.Column<int>(type: "integer", nullable: false),
                    MayMove = table.Column<bool>(type: "boolean", nullable: false),
                    FromYear = table.Column<int>(type: "integer", nullable: false),
                    ThroughYear = table.Column<int>(type: "integer", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_holiday_rule", x => x.Code);
                });
            // Technical activation starts in 2027: published 2026 occurrences remain untouched.
            migrationBuilder.Sql("""
                INSERT INTO platform.official_holiday_rule ("Code","Name","Calculation","Month","Day","EasterOffset","MayMove","FromYear","ThroughYear","LegalBasis","SourceUrl")
                SELECT code,name,calculation,month,day,offset_days,mobile,2027,NULL,'Ley 7544/2025, arts. 1–4','https://www.bacn.gov.py/leyes-paraguayas/12908/ley-n-7544-2025'
                FROM (VALUES
                ('NEW_YEAR','Año Nuevo','FIXED',1,1,0,false),
                ('HEROES','Héroes de la Patria','FIXED',3,1,0,true),
                ('WORKERS','Día de los Trabajadores','FIXED',5,1,0,false),
                ('INDEPENDENCE_14','Independencia Nacional — 14 de mayo','FIXED',5,14,0,false),
                ('INDEPENDENCE_15','Independencia Nacional — 15 de mayo','FIXED',5,15,0,false),
                ('CHACO','Paz del Chaco','FIXED',6,12,0,true),
                ('CONSTITUTION','Jura de la Constitución Nacional','FIXED',6,20,0,true),
                ('ASUNCION','Fundación de Asunción','FIXED',8,15,0,false),
                ('BOQUERON','Batalla de Boquerón','FIXED',9,29,0,true),
                ('CAACUPE','Virgen de Caacupé','FIXED',12,8,0,false),
                ('CHRISTMAS','Navidad','FIXED',12,25,0,false),
                ('HOLY_THURSDAY','Jueves Santo','EASTER',0,0,-3,false),
                ('GOOD_FRIDAY','Viernes Santo','EASTER',0,0,-2,false)
                ) AS rules(code,name,calculation,month,day,offset_days,mobile);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "official_holiday_rule",
                schema: "platform");
        }
    }
}
