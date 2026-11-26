using System.Data;
using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class CompanyDayOffService(CurrentTenantService tenant,TenantDbContextFactory factory,AuthenticationStateProvider authentication)
{
    public const string Code="DIA_LIBRE_EMPRESARIAL";
    private async Task<(string Schema,string User)> AdministratorAsync(CancellationToken ct)
    {
        var principal=(await authentication.GetAuthenticationStateAsync()).User;
        if(!principal.IsInRole(AppRoles.TenantAdministrator)) throw new UnauthorizedAccessException("Solo Administrador Tenant configura este beneficio.");
        var current=await tenant.GetRequiredAsync(ct);
        return(current.SchemaName,principal.FindFirstValue(ClaimTypes.NameIdentifier)??throw new UnauthorizedAccessException());
    }
    public async Task<List<CompanyDayOffPolicy>> GetPoliciesAsync(CancellationToken ct=default)
    {
        var access=await AdministratorAsync(ct);await using var db=factory.Create(access.Schema);
        return await db.CompanyDayOffPolicies.AsNoTracking().Include(x=>x.Company).OrderBy(x=>x.Company.LegalName).ThenByDescending(x=>x.EffectiveFrom).ToListAsync(ct);
    }
    public async Task SaveAsync(Guid company,DateOnly from,decimal days,CancellationToken ct=default)
    {
        var access=await AdministratorAsync(ct);
        if(days is not 0 and not .5m and not 1 || from<GlobalReferenceQueries.Today) throw new InvalidOperationException("Seleccione 0, 0.5 o 1 jornada y una vigencia desde hoy o futura; no se permiten versiones retroactivas.");
        await using var db=factory.Create(access.Schema);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE company_day_off_policy IN SHARE ROW EXCLUSIVE MODE",ct);
        if(!await db.Companies.AnyAsync(x=>x.Id==company,ct)) throw new UnauthorizedAccessException("Empresa no disponible en este tenant.");
        var last=await db.CompanyDayOffPolicies.Where(x=>x.CompanyId==company).OrderByDescending(x=>x.EffectiveFrom).FirstOrDefaultAsync(ct);
        if(last is not null&&from<=last.EffectiveFrom) throw new InvalidOperationException("La nueva vigencia debe ser posterior a la última versión; las versiones publicadas no se sobrescriben.");
        db.CompanyDayOffPolicies.Add(new(){CompanyId=company,EffectiveFrom=from,Days=days,CreatedByUserId=access.User});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public static async Task<LegalPermissionType?> RuleAsync(TenantDbContext db,Guid employment,DateOnly date,CancellationToken ct)
    {
        var company=await db.Employments.Where(x=>x.Id==employment).Select(x=>x.CompanyId).SingleAsync(ct);
        var policy=await db.CompanyDayOffPolicies.AsNoTracking().Where(x=>x.CompanyId==company&&x.EffectiveFrom<=date).OrderByDescending(x=>x.EffectiveFrom).FirstOrDefaultAsync(ct);
        if(policy is null||policy.Days==0) return null;
        return new(Code,"Día libre por política empresarial","Beneficios empresariales","Política empresarial","Beneficio anual independiente de vacaciones",
            $"{policy.Days:0.0} jornada por persona y año civil",policy.Days==1?"DAYS":"HOURS","Funcionarios de la empresa","Una solicitud por persona y año; sin acumulación","Sin documentación legal adicional","Fecha de disfrute desde vigencia de la política","Con goce de salario",false)
            {Engine="COMPANY_BENEFIT",QuantityUnit="DAYS",AnnualLimit=policy.Days,MaximumQuantity=policy.Days,CompanyPolicyId=policy.Id,PolicyCompanyId=company,RuleVersion=policy.Id.ToString(),RuleEffectiveFrom=policy.EffectiveFrom};
    }
    public static async Task<LegalPermissionType> RequiredRuleAsync(TenantDbContext db,Guid employment,DateOnly date,CancellationToken ct)
    {
        if(date<GlobalReferenceQueries.Today)throw new InvalidOperationException("El beneficio empresarial no puede solicitarse retroactivamente.");
        return await RuleAsync(db,employment,date,ct)??throw new InvalidOperationException("La política de la empresa no concede este beneficio para la fecha seleccionada.");
    }
    public static async Task<PermissionIntervalCalculation> CalculateAsync(TenantDbContext db,Guid employment,LegalPermissionType rule,LegalPermissionInput input,string unit,DateOnly start,DateOnly end,CancellationToken ct)
    {
        if(start!=end||start<GlobalReferenceQueries.Today||start<rule.RuleEffectiveFrom)throw new InvalidOperationException("Seleccione una sola fecha de disfrute vigente, desde hoy.");
        var day=(await WorkCalendarService.ResolvePermissionCalendarAsync(db,employment,start,start,ct)).Single();
        if(!day.CountsAsPermissionDay)throw new InvalidOperationException("El beneficio requiere una jornada que deba trabajarse; no corresponde a feriado o descanso.");
        if(rule.AnnualLimit==1)
        {
            if(unit!="DAYS")throw new InvalidOperationException("La política concede una jornada completa, por días.");
            return new(employment,"DAYS",start,start,null,null,1,0,[day]);
        }
        if(unit!="HOURS"||input.Variant is not "FIRST_HALF" and not "SECOND_HALF")throw new InvalidOperationException("Seleccione primera o segunda mitad de jornada.");
        if(day.EffectiveMinutes%2!=0)throw new InvalidOperationException("Esta jornada tiene un número impar de minutos. Configure un horario compatible con dos mitades iguales antes de solicitar el beneficio.");
        var half=day.EffectiveMinutes/2;
        var boundary=day.WorkStart.AddMinutes(half);
        if(day.BreakStart.HasValue && boundary>day.BreakStart.Value) boundary=boundary.AddMinutes((day.BreakEnd!.Value.ToTimeSpan()-day.BreakStart.Value.ToTimeSpan()).TotalMinutes);
        var from=input.Variant=="FIRST_HALF"?day.WorkStart:boundary;
        if(input.Variant=="SECOND_HALF"&&from==day.BreakStart)from=day.BreakEnd!.Value;
        var to=input.Variant=="FIRST_HALF"?boundary:day.WorkEnd;
        return new(employment,"HOURS",start,start,from,to,0,half,[day with{EffectiveMinutes=half}]);
    }
}
