using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

// A single resolver for administration, tenant views and all new calendar calculations.
public static class OfficialHolidayRecurrence
{
    public const string PendingMessage = "Feriados recurrentes determinados por ley disponibles. Traslados y extraordinarios sujetos a disposiciones oficiales; integridad anual pendiente de confirmación.";
    // Gregorian Meeus/Jones/Butcher computus, valid for the UI range 1900–2200.
    public static DateOnly Easter(int year)
    {
        var a=year%19; var b=year/100; var c=year%100; var d=b/4; var e=b%4;
        var f=(b+8)/25; var g=(b-f+1)/3; var h=(19*a+b-d-g+15)%30;
        var i=c/4; var k=c%4; var l=(32+2*e+2*i-h-k)%7; var m=(a+11*h+22*l)/451;
        var n=h+l-7*m+114; return new(year,n/31,n%31+1);
    }
    public static List<OfficialHolidayVersion> Resolve(int year, IEnumerable<OfficialHolidayRule> rules, IReadOnlyList<OfficialHolidayVersion> saved)
    {
        if(year is <1900 or >2200) throw new InvalidOperationException("Seleccione un año entre 1900 y 2200.");
        var result=saved.ToList();
        foreach(var rule in rules.Where(x=>x.FromYear<=year && (x.ThroughYear==null||x.ThroughYear>=year)))
        {
            var id=InitialGlobalReferenceSeed.Id($"recurrence:{rule.Code}:{year}");
            // Revision zero remains visible in history. Every consumer selects the highest revision
            // before checking status/dates, so an inactive or moved override cannot revive the base.
            var date=rule.Calculation=="EASTER"?Easter(year).AddDays(rule.EasterOffset):new DateOnly(year,rule.Month,rule.Day);
            result.Add(new(){Id=id,HolidayId=id,Revision=0,Year=year,Name=rule.Name,OriginalDate=date,ObservedDate=date,
                LegalBasis=rule.LegalBasis,SourceUrl=rule.SourceUrl,ChangeKind=rule.MayMove?"CALCULATED_MOBILE":"CALCULATED",ChangedByUserId="RECURRENCE",ChangedAtUtc=DateTimeOffset.UnixEpoch,
                ChangeReason=rule.MayMove?"Fecha nominal legal; puede cambiar mediante disposición oficial.":"Recurrencia determinada por ley; no implica verificación de decretos futuros."});
        }
        return result;
    }
    public static async Task<List<OfficialHolidayVersion>> ReadAsync(DbContext db,int year,CancellationToken ct=default)
    {
        var rules=await db.Database.SqlQueryRaw<OfficialHolidayRule>("SELECT * FROM platform.official_holiday_rule").ToListAsync(ct);
        var saved=await db.Database.SqlQueryRaw<OfficialHolidayVersion>("SELECT * FROM platform.official_holiday_version").ToListAsync(ct);
        return Resolve(year,rules,saved);
    }
    public static string State(OfficialHolidayVersion x)=>x.Kind=="MOVED"?"Traslado confirmado":x.Kind=="EXTRAORDINARY"?"Extraordinario publicado":x.ChangeKind=="CALCULATED_MOBILE"?"Calculado · fecha susceptible de traslado":x.ChangeKind=="CALCULATED"?"Recurrente calculado":"Fecha publicada / revisión administrativa";
}
