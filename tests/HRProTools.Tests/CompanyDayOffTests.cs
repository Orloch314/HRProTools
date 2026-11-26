using System.Text.Json;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRProTools.Tests;

public sealed partial class Phase4VacationRequestTests
{
    private static readonly DateOnly BenefitMonday=new(2027,3,8);
    private static LegalPermissionInput BenefitInput(string half="FIRST_HALF")=>new(){Code=CompanyDayOffService.Code,Variant=half};
    private static async Task BenefitPolicyAsync(RequestServiceFixture f,Guid company,decimal days,DateOnly? from=null)
    {
        f.AuthenticateAs(f.AdministratorUser,AppRoles.TenantAdministrator);
        await f.Scope.ServiceProvider.GetRequiredService<CompanyDayOffService>().SaveAsync(company,from??GlobalReferenceQueries.Today,days);
        f.AuthenticateAs(f.User,AppRoles.Employee);
    }
    private static async Task BenefitApproveAsync(RequestServiceFixture f,Guid id)
    {
        var service=f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        f.AuthenticateAs(f.ApproverUser,AppRoles.Approver);await service.ApproveAsync(id,"Autorización sintética");
        f.AuthenticateAs(f.HumanResourcesUser,AppRoles.HumanResources);await service.ApproveAsync(id,"Confirmación sintética");
        f.AuthenticateAs(f.User,AppRoles.Employee);
    }
    [Fact]
    public async Task PerpetualCalendarCalculatesAllFutureYearsWithoutWritingOccurrences()
    {
        await using var f=await RequestServiceFixture.CreateAsync();var ids=await f.SeedAsync();
        f.AuthenticateAs(f.AdministratorUser,AppRoles.TenantAdministrator);
        var admin=f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>();
        var global=f.Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count=await global.OfficialHolidayVersions.CountAsync();
        var original=(await admin.GetHolidaysAsync(2026)).Versions;
        Assert.Equal(14,original.Count);Assert.DoesNotContain(original,x=>x.Revision==0);
        foreach(var year in new[]{2027,2028,2038,2100,2200})
        {
            var result=await admin.GetHolidaysAsync(year);var current=result.Versions.GroupBy(x=>x.HolidayId).Select(g=>g.MaxBy(x=>x.Revision)!).Where(x=>x.Year==year&&x.IsActive).ToList();
            Assert.Equal(13,current.Count);Assert.Equal(13,current.Select(x=>x.HolidayId).Distinct().Count());
            Assert.Contains(current,x=>x.OriginalDate==new DateOnly(year,6,20));
            Assert.All(current,x=>Assert.Equal(x.OriginalDate,x.ObservedDate));
            Assert.Equal(4,current.Count(x=>OfficialHolidayRecurrence.State(x).Contains("susceptible")));
            var again=await admin.GetHolidaysAsync(year);Assert.Equal(current.Select(x=>x.Id).Order(),again.Versions.Where(x=>x.Year==year).Select(x=>x.Id).Order());
        }
        Assert.Equal(new DateOnly(2027,3,25),(await admin.GetHolidaysAsync(2027)).Versions.Single(x=>x.Name=="Jueves Santo").OriginalDate);
        Assert.Equal(new DateOnly(2028,4,13),(await admin.GetHolidaysAsync(2028)).Versions.Single(x=>x.Name=="Jueves Santo").OriginalDate);
        Assert.Equal(new DateOnly(2028,4,14),(await admin.GetHolidaysAsync(2028)).Versions.Single(x=>x.Name=="Viernes Santo").OriginalDate);
        Assert.Equal(count,await global.OfficialHolidayVersions.CountAsync());
        var calendar=await f.Scope.ServiceProvider.GetRequiredService<TenantCalendarService>().GetAsync(2027,ids.CompanyId);
        Assert.Equal(13,calendar.Days.Count);Assert.False(calendar.Confirmed);Assert.Contains("recurrentes",calendar.Verification);
    }
    [Fact]
    public async Task PerpetualCalendarOverridesMovesDeactivationAndExtraordinaryStayInTheirYear()
    {
        await using var f=await RequestServiceFixture.CreateAsync();var ids=await f.SeedAsync();
        var admin=f.Scope.ServiceProvider.GetRequiredService<ReferenceAdministrationService>();
        f.AuthenticateAs(f.AdministratorUser,AppRoles.GlobalAdministrator);
        var nominal=(await admin.GetHolidaysAsync(2194)).Versions.Single(x=>x.Name=="Navidad");
        var extra=TestHoliday(new(2194,2,10));extra.Kind="EXTRAORDINARY";extra.Disposition="Disposición sintética";
        try
        {
            var moved=Revision(nominal,new(2194,12,24),"OFFICIAL_MOVE");var saved=await admin.SaveHolidayAsync(moved,nominal.Id);
            await admin.SaveHolidayAsync(extra,null);
            var current=(await admin.GetHolidaysAsync(2194)).Versions.GroupBy(x=>x.HolidayId).Select(g=>g.MaxBy(x=>x.Revision)!).ToList();
            Assert.Contains((await admin.GetHolidaysAsync(2194)).Versions,x=>x.HolidayId==nominal.HolidayId&&x.Revision==0);
            Assert.Single(current,x=>x.HolidayId==nominal.HolidayId);Assert.Equal(new DateOnly(2194,12,24),current.Single(x=>x.HolidayId==nominal.HolidayId).ObservedDate);
            await using var db=f.CreateTenantDb();var dates=await GlobalReferenceQueries.HolidaysAsync(db,ids.CompanyId,new(2194,12,24),new(2194,12,25));Assert.Single(dates);Assert.Contains(new DateOnly(2194,12,24),dates.Keys);
            var next=(await admin.GetHolidaysAsync(2195)).Versions.Where(x=>x.Year==2195).ToList();Assert.Equal(13,next.Count);Assert.DoesNotContain(next,x=>x.Kind=="EXTRAORDINARY");
            var inactive=Revision(moved,moved.ObservedDate);inactive.IsActive=false;await admin.SaveHolidayAsync(inactive,saved);
            Assert.Empty(await GlobalReferenceQueries.HolidaysAsync(db,ids.CompanyId,new(2194,12,24),new(2194,12,25)));
            f.AuthenticateAs(f.AdministratorUser,AppRoles.TenantAdministrator);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>admin.SaveHolidayAsync(Revision(moved,new(2194,12,23)),saved));
        }
        finally{await CleanReferencesAsync(f,holiday:nominal.HolidayId,year:2194);await CleanReferencesAsync(f,holiday:extra.HolidayId);}
    }
    [Fact]
    public async Task CompanyBenefitPoliciesAreScopedVersionedAndBackendGuarded()
    {
        await using var f=await RequestServiceFixture.CreateAsync();var ids=await f.SeedAsync();await using var other=await RequestServiceFixture.CreateAsync();var otherIds=await other.SeedAsync();
        var policy=f.Scope.ServiceProvider.GetRequiredService<CompanyDayOffService>();var requests=f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        Assert.DoesNotContain(await requests.GetAvailableLegalTypesAsync(),x=>x.Code==CompanyDayOffService.Code);
        foreach(var role in new[]{AppRoles.Employee,AppRoles.Approver,AppRoles.HumanResources,AppRoles.GlobalAdministrator})
        {f.AuthenticateAs(f.AdministratorUser,role);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.SaveAsync(ids.CompanyId,GlobalReferenceQueries.Today,1));}
        f.AuthenticateAs(f.AdministratorUser,AppRoles.TenantAdministrator);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.SaveAsync(otherIds.CompanyId,GlobalReferenceQueries.Today,1));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>policy.SaveAsync(ids.CompanyId,GlobalReferenceQueries.Today.AddDays(-1),1));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>policy.SaveAsync(ids.CompanyId,GlobalReferenceQueries.Today,.25m));
        await policy.SaveAsync(ids.CompanyId,GlobalReferenceQueries.Today,0);
        f.AuthenticateAs(f.User,AppRoles.Employee);Assert.DoesNotContain(await requests.GetAvailableLegalTypesAsync(),x=>x.Code==CompanyDayOffService.Code);
        await BenefitPolicyAsync(f,ids.CompanyId,.5m,GlobalReferenceQueries.Today.AddDays(1));
        Assert.DoesNotContain(await requests.GetAvailableLegalTypesAsync(),x=>x.Code==CompanyDayOffService.Code);
        Assert.Equal("HOURS",(await requests.GetAvailableLegalTypesAsync(date:BenefitMonday)).Single(x=>x.Code==CompanyDayOffService.Code).Unit);
        var id=await AuditCreateAsync(f,BenefitInput(),BenefitMonday,BenefitMonday,"HOURS");
        var before=await SnapshotBenefitAsync(f,id);
        await BenefitPolicyAsync(f,ids.CompanyId,1,GlobalReferenceQueries.Today.AddDays(2));
        Assert.Equal(before,await SnapshotBenefitAsync(f,id));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,BenefitInput(),BenefitMonday.AddDays(1),BenefitMonday.AddDays(1)));
        f.AuthenticateAs(f.AdministratorUser,AppRoles.TenantAdministrator);Assert.Equal(3,(await policy.GetPoliciesAsync()).Count);
        await using var db=f.CreateTenantDb();var another=await db.Companies.SingleAsync(x=>x.Id!=ids.CompanyId);
        Assert.False(await db.CompanyDayOffPolicies.AnyAsync(x=>x.CompanyId==another.Id));
        Assert.DoesNotContain(await other.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>().GetAvailableLegalTypesAsync(),x=>x.Code==CompanyDayOffService.Code);
        f.AuthenticateAs(f.AdministratorUser,AppRoles.TenantAdministrator);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>AuditCreateAsync(f,BenefitInput(),BenefitMonday,BenefitMonday));
    }
    private static async Task<string> SnapshotBenefitAsync(RequestServiceFixture f,Guid id)
    {await using var db=f.CreateTenantDb();return await db.Database.SqlQuery<string>($"SELECT to_jsonb(r)::text AS \"Value\" FROM permission_request r WHERE \"Id\"={id}").SingleAsync();}
    private static async Task<string> VacationDaySnapshotAsync(RequestServiceFixture f,Guid id)
    {await using var db=f.CreateTenantDb();return JsonSerializer.Serialize(await db.Database.SqlQuery<string>($"SELECT to_jsonb(d)::text AS \"Value\" FROM vacation_request_day d WHERE \"VacationRequestId\"={id} ORDER BY \"Date\"").ToListAsync());}
    [Fact]
    public async Task CompanyBenefitFullDayExcludesExactlyOneVacationDayAndPreservesFifoEvidence()
    {
        await using var f=await RequestServiceFixture.CreateAsync();var ids=await f.SeedAsync();await BenefitPolicyAsync(f,ids.CompanyId,1);
        var input=BenefitInput();var wednesday=BenefitMonday.AddDays(2);var id=await AuditCreateAsync(f,input,wednesday,wednesday);
        var pending=await AuditBalanceAsync(f,input,wednesday);Assert.Equal(1,pending.Buckets.Single(x=>x.Unit=="DAYS").Reserved);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Scope.ServiceProvider.GetRequiredService<VacationRequestService>().PreviewSelfAsync(BenefitMonday,BenefitMonday.AddDays(4)));
        await BenefitApproveAsync(f,id);var used=await AuditBalanceAsync(f,input,wednesday);Assert.Equal(1,used.Buckets.Single(x=>x.Unit=="DAYS").Used);Assert.True(used.Exhausted);
        var vacation=f.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var preview=await vacation.PreviewSelfAsync(BenefitMonday,BenefitMonday.AddDays(4));Assert.Equal(5,preview.Dates.Count);Assert.Equal(4,preview.DaysToReserve);
        Assert.Equal(id,preview.Dates.Single(x=>x.Date==wednesday).CompanyDayOffRequestId);Assert.False(preview.Dates.Single(x=>x.Date==wednesday).IsHoliday);
        // Synthetic balance of exactly 12 available days, with no prior consumption.
        await using(var db=f.CreateTenantDb()){await db.VacationMovements.ExecuteDeleteAsync();db.VacationMovements.Add(new(){VacationPeriodId=ids.FirstPeriodId,Type=VacationMovementTypes.Credit,LegalDays=12,OperationKey="benefit-credit"});await db.SaveChangesAsync();}
        var vacationId=await vacation.CreateSelfAsync(Guid.NewGuid(),BenefitMonday,BenefitMonday.AddDays(4));
        string snapshot;await using(var db=f.CreateTenantDb())
        {
            var saved=await db.VacationRequests.AsNoTracking().Include(x=>x.Dates).SingleAsync(x=>x.Id==vacationId);Assert.Equal(4,saved.RequestedDays);Assert.Equal(id,saved.Dates.Single(x=>x.Date==wednesday).CompanyDayOffRequestId);
            var balance=await VacationLedgerQueries.GetEmploymentBalanceAsync(db,ids.EmploymentId);Assert.Equal(12,balance.Recognized);Assert.Equal(4,balance.Reserved);Assert.Equal(8,balance.Available);
            Assert.Equal(2,await db.VacationMovements.CountAsync());snapshot=await VacationDaySnapshotAsync(f,vacationId);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>().CancelSelfAsync(id,"Conflicto controlado"));
        f.AuthenticateAs(f.ApproverUser,AppRoles.Approver);await vacation.ApproveAsync(vacationId,"Autorizada");f.AuthenticateAs(f.HumanResourcesUser,AppRoles.HumanResources);await vacation.ApproveAsync(vacationId,"Aprobada");
        await using(var db=f.CreateTenantDb())
        {
            var balance=await VacationLedgerQueries.GetEmploymentBalanceAsync(db,ids.EmploymentId);Assert.Equal(4,balance.Consumed);Assert.Equal(8,balance.Available);Assert.Equal(3,await db.VacationMovements.CountAsync());
            var consumption=await db.VacationMovements.SingleAsync(x=>x.Type==VacationMovementTypes.Consume);Assert.Equal(4,consumption.LegalDays);Assert.NotNull(consumption.SourceMovementId);
            Assert.Equal(snapshot,await VacationDaySnapshotAsync(f,vacationId));
        }
        // A different person in the same company still works the Wednesday.
        await using(var db=f.CreateTenantDb())
        {
            var person=new Person{DocumentNumber="B2",NormalizedDocumentNumber="B2",GivenNames="Otra",FamilyNames="Persona"};db.People.Add(person);await db.SaveChangesAsync();
            var second=await AuditEmploymentAsync(f,ids.CompanyId,person.Id,GlobalReferenceQueries.Today,"B2",false);
            Assert.Equal(5,(await WorkCalendarService.CalculateVacationIntervalAsync(db,second,BenefitMonday,BenefitMonday.AddDays(4))).DaysToReserve);
        }
        f.AuthenticateAs(f.User,AppRoles.Employee);await vacation.CancelSelfAsync(vacationId);await f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>().CancelSelfAsync(id,"Conflicto resuelto explícitamente");
    }
    [Theory]
    [InlineData("FIRST_HALF",8,12)]
    [InlineData("SECOND_HALF",13,17)]
    public async Task CompanyBenefitHalfDayUsesEffectiveHoursAndBlocksFullDayVacation(string half,int from,int to)
    {
        await using var f=await RequestServiceFixture.CreateAsync();var ids=await f.SeedAsync();await BenefitPolicyAsync(f,ids.CompanyId,.5m);
        var input=BenefitInput(half);var requests=f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var preview=await requests.PreviewSelfAsync("HOURS",BenefitMonday,BenefitMonday,null,null,legal:input);
        Assert.Equal(240,preview.RequestedMinutes);Assert.Equal(new TimeOnly(from,0),preview.StartTime);Assert.Equal(new TimeOnly(to,0),preview.EndTime);
        var id=await AuditCreateAsync(f,input,BenefitMonday,BenefitMonday,"HOURS");
        Assert.Equal(id,await requests.CreateSelfAsync(id,"PAID","HOURS",BenefitMonday,BenefitMonday,null,null,"Reintento",legal:input));
        await BenefitApproveAsync(f,id);
        await using(var db=f.CreateTenantDb()){var day=await db.PermissionRequestDays.SingleAsync();Assert.Equal(.5m,day.LegalDayFraction);Assert.Equal(240,day.EffectiveMinutes);Assert.False(day.IsHoliday);Assert.Empty(await db.CompanyHolidays.ToListAsync());}
        var vacation=f.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var ex=await Assert.ThrowsAsync<InvalidOperationException>(()=>vacation.PreviewSelfAsync(BenefitMonday,BenefitMonday.AddDays(1)));Assert.Contains("parcial",ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>vacation.CreateSelfAsync(Guid.NewGuid(),BenefitMonday,BenefitMonday.AddDays(1)));
        await requests.CancelSelfAsync(id,"Liberar beneficio");await requests.CancelSelfAsync(id,"Reintento");Assert.Equal(.5m,(await AuditBalanceAsync(f,input,BenefitMonday)).Buckets.Single(x=>x.Unit=="DAYS").Available);
    }
    [Fact]
    public async Task CompanyBenefitRejectCancelAndConcurrencyCannotDuplicateAnnualUseOrReentry()
    {
        await using var f=await RequestServiceFixture.CreateAsync();var ids=await f.SeedAsync();await BenefitPolicyAsync(f,ids.CompanyId,1);var input=BenefitInput();
        async Task<bool> Send(DateOnly day){try{await AuditCreateAsync(f,input,day,day);return true;}catch(InvalidOperationException){return false;}}
        Assert.Single(await Task.WhenAll(Send(BenefitMonday),Send(BenefitMonday.AddDays(1))),x=>x);
        Guid id;await using(var db=f.CreateTenantDb()){Assert.Equal(1,await db.PermissionRequests.CountAsync());id=await db.PermissionRequests.Select(x=>x.Id).SingleAsync();}
        f.AuthenticateAs(f.ApproverUser,AppRoles.Approver);var requests=f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();await requests.RejectAsync(id,"Rechazo sintético");await requests.RejectAsync(id,"Reintento");f.AuthenticateAs(f.User,AppRoles.Employee);
        Assert.Equal(1,(await AuditBalanceAsync(f,input,BenefitMonday)).Buckets.Single(x=>x.Unit=="DAYS").Available);
        id=await AuditCreateAsync(f,input,BenefitMonday.AddDays(2),BenefitMonday.AddDays(2));await BenefitApproveAsync(f,id);await requests.CancelSelfAsync(id,"Anulación aprobada");await requests.CancelSelfAsync(id,"Reintento");
        id=await AuditCreateAsync(f,input,BenefitMonday.AddDays(3),BenefitMonday.AddDays(3));await BenefitApproveAsync(f,id);
        Guid person;await using(var db=f.CreateTenantDb())person=await db.Employments.Select(x=>x.PersonId).SingleAsync();
        var reentry=await AuditEmploymentAsync(f,ids.CompanyId,person,BenefitMonday.AddDays(4),"REINGRESO",true);
        var global=f.Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var user=await global.Users.SingleAsync(x=>x.Id==f.User.Id);user.ActiveEmploymentId=reentry;await global.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,BenefitMonday.AddDays(4),BenefitMonday.AddDays(4)));
        var balance=await AuditBalanceAsync(f,input,BenefitMonday.AddDays(4));Assert.Equal(1,balance.Buckets.Single(x=>x.Unit=="DAYS").Used);Assert.Contains(balance.Requests,x=>x.EmployeeNumber=="99");
        Assert.NotNull(await requests.PreviewSelfAsync("DAYS",new(2028,3,6),new(2028,3,6),null,null,legal:input));
    }
    [Fact]
    public async Task CompanyBenefitRejectsHolidaysRestAbsencesAndExistingRequestsWithoutChangingHistory()
    {
        await using var f=await RequestServiceFixture.CreateAsync();var ids=await f.SeedAsync();await BenefitPolicyAsync(f,ids.CompanyId,1);var input=BenefitInput();
        foreach(var day in new[]{new DateOnly(2027,1,1),BenefitMonday.AddDays(5)})await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,day,day));
        await using(var db=f.CreateTenantDb()){db.CompanyHolidays.Add(new(){CompanyId=ids.CompanyId,Date=BenefitMonday,Name="Adicional sintético"});await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,BenefitMonday,BenefitMonday));
        var vacation=f.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();var vacationId=await vacation.CreateSelfAsync(Guid.NewGuid(),BenefitMonday.AddDays(1),BenefitMonday.AddDays(2));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,BenefitMonday.AddDays(1),BenefitMonday.AddDays(1)));
        f.AuthenticateAs(f.ApproverUser,AppRoles.Approver);await vacation.ApproveAsync(vacationId,"Autorizar");f.AuthenticateAs(f.HumanResourcesUser,AppRoles.HumanResources);await vacation.ApproveAsync(vacationId,"Confirmar");f.AuthenticateAs(f.User,AppRoles.Employee);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,BenefitMonday.AddDays(2),BenefitMonday.AddDays(2)));
        var permissions=f.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();var permission=await permissions.CreateSelfAsync(Guid.NewGuid(),"PAID","HOURS",BenefitMonday.AddDays(3),BenefitMonday.AddDays(3),new(8,0),new(9,0),"Permiso previo");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,BenefitMonday.AddDays(3),BenefitMonday.AddDays(3)));
        await BenefitApproveAsync(f,permission);await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,BenefitMonday.AddDays(3),BenefitMonday.AddDays(3)));
        await f.Scope.ServiceProvider.GetRequiredService<AbsenceService>().CreateSelfAsync(BenefitMonday.AddDays(4),BenefitMonday.AddDays(4),AbsenceTypes.OtherJustified,"Ausencia sintética");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AuditCreateAsync(f,input,BenefitMonday.AddDays(4),BenefitMonday.AddDays(4)));
        await using var verify=f.CreateTenantDb();Assert.False(await verify.PermissionRequests.AnyAsync(x=>x.LegalCode==CompanyDayOffService.Code));Assert.Equal(2,(await verify.VacationRequests.SingleAsync()).RequestedDays);Assert.All(await verify.VacationRequestDays.ToListAsync(),x=>Assert.Null(x.CompanyDayOffRequestId));
    }
}
