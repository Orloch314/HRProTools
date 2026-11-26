using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HRProTools.Web.Services;

// Frozen bootstrap for the first global migration. Published changes are new DB versions, never edits here.
public static class InitialGlobalReferenceSeed
{
    public const string HolidayLawUrl = "https://www.bacn.gov.py/leyes-paraguayas/12908/ley-n-7544-2025-que-determina-los-feriados-nacionales-de-la-rep-blica-del-paraguay-se-establecen-los-feriados-m-viles-y-se-faculta-al-poder-ejecutivo-a-instituir-otros-feriados-en-situaciones-especiales";
    public static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("HRProTools-reference-v1:" + key))[..16]);
    public static void Insert(MigrationBuilder migration)
    {
        var timestamp = new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero);
        foreach (var original in LegalPermissionCatalog.All)
        {
            var id = Id(original.Code);
            var rule = original with
            {
                RuleVersion = id.ToString(),
                Description = original.Requirements,
                SourceUrl = LegalSource(original.Code),
                Engine = original.Protected || original.Code == "REPOSO_MEDICO" ? "SPECIAL" : "PARAMETRIC",
                IsPaid = original.Code != "PERMISO_SIN_GOCE",
                QuantityUnit = "DAYS"
            };
            migration.InsertData("legal_permission_version", new[] { "Id", "Code", "Revision", "ValidFrom", "ValidTo", "IsActive", "RuleJson", "ChangeReason", "ChangedByUserId", "ChangedAtUtc" },
                new object?[] { id, rule.Code, 1, rule.RuleEffectiveFrom, null, true, JsonSerializer.Serialize(rule), "Adaptación del catálogo existente; conserva reglas y códigos.", "SYSTEM_MIGRATION", timestamp }, "platform");
        }
        foreach (var holiday in Holidays2026())
            migration.InsertData("official_holiday_version", new[] { "Id", "HolidayId", "Revision", "Year", "Name", "OriginalDate", "ObservedDate", "Kind", "IsActive", "LegalBasis", "SourceUrl", "Disposition", "ChangeKind", "ChangeReason", "ChangedByUserId", "ChangedAtUtc" },
                new object?[] { Id(holiday.Name + ":v1"), Id(holiday.Name + ":2026"), 1, 2026, holiday.Name, holiday.OriginalDate, holiday.ObservedDate, holiday.Kind, true, "Ley 7544/2025", holiday.SourceUrl, holiday.Disposition, "INITIAL", "Carga inicial documentada de 2026.", "SYSTEM_MIGRATION", timestamp }, "platform");
        migration.InsertData("official_calendar_year", new[] { "Year", "IsConfirmed", "VerificationNote", "SourceUrl", "ChangedByUserId", "ChangedAtUtc" },
            new object[] { 2026, false, "Base legal y disposiciones verificadas. Pendiente de confirmación administrativa de integridad anual; no extrapolado a otros años.", HolidayLawUrl, "SYSTEM_MIGRATION", timestamp }, "platform");
    }
    public static IEnumerable<OfficialHolidayVersion> Holidays2026()
    {
        var fixedDates = new (int Month, int Day, string Name)[] { (1, 1, "Año Nuevo"), (5, 1, "Día de los Trabajadores"), (5, 14, "Independencia Nacional — 14 de mayo"), (5, 15, "Independencia Nacional — 15 de mayo"), (6, 12, "Paz del Chaco"), (8, 15, "Fundación de Asunción"), (12, 8, "Virgen de Caacupé"), (12, 25, "Navidad") };
        foreach (var x in fixedDates) yield return new() { Name = x.Name, OriginalDate = new(2026, x.Month, x.Day), ObservedDate = new(2026, x.Month, x.Day), SourceUrl = HolidayLawUrl };
        const string bcp = "https://www.bcp.gov.py/documents/20117/86461/Feriados%2BSML%2B-%2B2026%2B-%2BV190226.pdf/b16b80fa-aa45-e020-b067-25590cd7c32b?t=1781095767235";
        foreach (var x in new[] { (2, "Jueves Santo"), (3, "Viernes Santo") }) yield return new() { Name = x.Item2, OriginalDate = new(2026, 4, x.Item1), ObservedDate = new(2026, 4, x.Item1), SourceUrl = bcp };
        yield return new() { Name = "Héroes de la Patria", OriginalDate = new(2026, 3, 1), ObservedDate = new(2026, 3, 2), Kind = "MOVED", Disposition = "Decreto 5508/2026", SourceUrl = "https://www.radionacional.gov.py/2026/02/17/ejecutivo-decreta-traslado-del-feriado-del-dia-de-los-heroes-al-lunes-2-de-marzo/" };
        yield return new() { Name = "Jura de la Constitución Nacional", OriginalDate = new(2026, 6, 20), ObservedDate = new(2026, 6, 22), Kind = "MOVED", Disposition = "Decreto 6215/2026", SourceUrl = "https://www.ip.gov.py/ip/2026/06/09/gobierno-traslada-feriado-por-la-jura-de-la-constitucion-de-1992-al-lunes-22/" };
        yield return new() { Name = "Batalla de Boquerón", OriginalDate = new(2026, 9, 29), ObservedDate = new(2026, 9, 28), Kind = "MOVED", Disposition = "Decreto 6601/2026", SourceUrl = "https://www.contrataciones.gov.py/licitaciones/convocatoria/1f18f46a-8ce5-6880-9680-af6dce24a6d2/verificaciones/1788940704.html" };
        yield return new() { Name = "Feriado extraordinario nacional — 30 de junio", OriginalDate = new(2026, 6, 30), ObservedDate = new(2026, 6, 30), Kind = "EXTRAORDINARY", Disposition = "Decreto 6280/2026", SourceUrl = "https://ministeriopublico.gov.py/nota/ministerio-publico-declara-feriado-institucional-con-excepciones-para-garantizar-servicios-esenciales-17544" };
    }
    private static string LegalSource(string code) => code switch
    {
        "MATRIMONIO" or "LUTO_FAMILIAR" => "https://www.bacn.gov.py/leyes-paraguayas/3438/modifica-el-articulo-62-inciso-j-de-la-ley-n-21393-que-establece-el-codigo-del-trabajo",
        "MATERNIDAD" => "https://www.bacn.gov.py/leyes-paraguayas/12838/ley-n-7534-2025-que-modifica-y-amplia-los-articulos-2-y-11-de-la-ley-n-5508-2015",
        "PATERNIDAD" or "ADOPCION" or "LACTANCIA" => "https://www.bacn.gov.py/leyes-paraguayas/4428/promocion-proteccion-de-la-maternidad-y-apoyo-a-la-lactancia-materna",
        "CONTROL_PRENATAL" => "https://mtess.gov.py/?p=30138",
        "PAP_MAMOGRAFIA" => "https://www.bacn.gov.py/leyes-paraguayas/8937/ley-n-6211-modi",
        "PREVENCION_PROSTATA_TESTICULOS_PENE_COLON" => "https://silpy.congreso.gov.py/web/ley/146526",
        "ACOMPANAMIENTO_DISCAPACIDAD" => "https://www.pj.gov.py/descargar/ID1-945_gaceta_oficial_ley_n_7371_2025.pdf",
        _ => "https://www.bacn.gov.py/leyes-paraguayas/2608/ley-n-213-establece-el-codi-"
    };
}
