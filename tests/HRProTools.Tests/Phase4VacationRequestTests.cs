using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HRProTools.Tests;

public sealed class Phase4VacationRequestTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    [Fact]
    public void IndividualHireRequiresExplicitWorkSchedule()
    {
        var form = new EmployeeForm
        {
            DocumentNumber = "1234567",
            GivenNames = "Ana",
            FamilyNames = "Prueba",
            CompanyId = Guid.NewGuid(),
            DepartmentId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            GrossSalary = 1_000_000,
            WorkScheduleId = null
        };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(form, new ValidationContext(form), results, true));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(EmployeeForm.WorkScheduleId)));
    }

    [Theory]
    [InlineData(false, false, 5)]
    [InlineData(true, false, 6)]
    [InlineData(true, true, 7)]
    public async Task CalendarCountsOnlyContractualWorkdaysIncludingSaturdayAndSunday(
        bool saturday,
        bool sunday,
        int expected)
    {
        await using var fixture = await TenantSchemaFixture.CreateAsync();
        var ids = await fixture.SeedEmploymentAsync(saturday, sunday);

        await using var db = fixture.CreateDb();
        var result = await WorkCalendarService.CalculateVacationIntervalAsync(
            db, ids.EmploymentId, Monday, Sunday);

        Assert.Equal(expected, result.DaysToReserve);
        Assert.Equal(sunday, result.Dates.Single(x => x.Date == Sunday).CountsAsVacationDay);
    }

    [Fact]
    public async Task ApplicableCompanyHolidaysAreExcluded()
    {
        await using var fixture = await TenantSchemaFixture.CreateAsync();
        var ids = await fixture.SeedEmploymentAsync(false, false);
        await using (var db = fixture.CreateDb())
        {
            db.CompanyHolidays.AddRange(
                new CompanyHoliday { CompanyId = ids.CompanyId, Date = Monday.AddDays(1), Name = "Feriado uno" },
                new CompanyHoliday { CompanyId = ids.CompanyId, Date = Monday.AddDays(3), Name = "Feriado dos" });
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateDb();
        var result = await WorkCalendarService.CalculateVacationIntervalAsync(
            verify, ids.EmploymentId, Monday, Sunday);

        Assert.Equal(3, result.DaysToReserve);
        Assert.Equal(2, result.Dates.Count(x => x.IsHoliday && !x.CountsAsVacationDay));
    }

    [Fact]
    public async Task CalendarUsesTheScheduleEffectiveOnEachDate()
    {
        await using var fixture = await TenantSchemaFixture.CreateAsync();
        var ids = await fixture.SeedEmploymentAsync(false, false, Monday, Monday.AddDays(2));
        await using (var db = fixture.CreateDb())
        {
            var second = new WorkSchedule
            {
                Name = "Martes a sábado",
                StartTime = new(8, 0),
                EndTime = new(17, 0),
                Monday = false,
                Tuesday = true,
                Wednesday = true,
                Thursday = true,
                Friday = true,
                Saturday = true,
                Sunday = false
            };
            db.WorkSchedules.Add(second);
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule
            {
                EmploymentId = ids.EmploymentId,
                WorkSchedule = second,
                ValidFrom = Monday.AddDays(3)
            });
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateDb();
        var result = await WorkCalendarService.CalculateVacationIntervalAsync(
            verify, ids.EmploymentId, Monday, Sunday);

        Assert.Equal(6, result.DaysToReserve);
        Assert.All(result.Dates.Take(3), x => Assert.Equal("Jornada de prueba", x.WorkScheduleName));
        Assert.All(result.Dates.Skip(3), x => Assert.Equal("Martes a sábado", x.WorkScheduleName));
        Assert.False(result.Dates.Single(x => x.Date == Sunday).CountsAsVacationDay);
    }

    [Fact]
    public async Task RequestSnapshotsDatesAndFifoStartsWithPartiallyConsumedOldestPeriod()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();

        var preview = await service.PreviewSelfAsync(Monday, Monday.AddDays(7));
        Assert.Equal(6, preview.DaysToReserve);
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));

        await using (var db = fixture.CreateTenantDb())
        {
            var request = await db.VacationRequests.Include(x => x.Dates).Include(x => x.Allocations)
                .SingleAsync(x => x.Id == requestId);
            Assert.Equal(8, request.Dates.Count);
            Assert.Equal(6, request.Dates.Count(x => x.CountsAsVacationDay));
            Assert.Equal(2, request.Allocations.Count);
            Assert.Equal(4, request.Allocations.Single(x => x.VacationPeriodId == ids.FirstPeriodId).LegalDays);
            Assert.Equal(2, request.Allocations.Single(x => x.VacationPeriodId == ids.SecondPeriodId).LegalDays);

            var schedule = await db.WorkSchedules.SingleAsync();
            schedule.Monday = false;
            db.CompanyHolidays.Add(new CompanyHoliday { CompanyId = ids.CompanyId, Date = Monday.AddDays(1), Name = "Feriado posterior" });
            await db.SaveChangesAsync();
        }

        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.ApproveAsync(requestId, "Aprobada");
        await service.ApproveAsync(requestId, "Aprobada");
        await using (var intermediate = fixture.CreateTenantDb())
        {
            Assert.Equal(VacationRequestStatuses.Pending, (await intermediate.VacationRequests.SingleAsync(x => x.Id == requestId)).Status);
            Assert.Equal(6, (await VacationLedgerQueries.GetEmploymentBalanceAsync(intermediate, ids.EmploymentId)).Reserved);
            Assert.Equal(0, await intermediate.VacationMovements.CountAsync(x => x.OperationKey == $"consume:vacation-request:{requestId:N}"));
        }
        fixture.AuthenticateAs(fixture.HumanResourcesUser, AppRoles.HumanResources);
        await service.ApproveAsync(requestId, "Aprobación final");
        await service.ApproveAsync(requestId, "Aprobación final");
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee);
        await service.CancelSelfAsync(requestId);
        await service.CancelSelfAsync(requestId);

        var rejectedId = Guid.NewGuid();
        await service.CreateSelfAsync(rejectedId, Monday.AddDays(14), Monday.AddDays(21));
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.RejectAsync(rejectedId, "Rechazo de prueba");
        await service.RejectAsync(rejectedId, "Rechazo de prueba");

        await using var verify = fixture.CreateTenantDb();
        var original = await verify.VacationRequests.Include(x => x.Dates).SingleAsync(x => x.Id == requestId);
        Assert.Equal(VacationRequestStatuses.Cancelled, original.Status);
        Assert.Equal(6, original.RequestedDays);
        Assert.Equal(6, original.Dates.Count(x => x.CountsAsVacationDay));
        Assert.Equal(2, await verify.VacationMovements.CountAsync(x => x.OperationKey == $"consume:vacation-request:{requestId:N}"));
        Assert.Equal(2, await verify.VacationMovements.CountAsync(x => x.OperationKey == $"reversal:consume:vacation-request:{requestId:N}"));
        Assert.Equal(1, await verify.VacationMovements.CountAsync(x => x.OperationKey == $"release:vacation-request:{rejectedId:N}"));
        Assert.Equal(VacationRequestStatuses.Rejected, (await verify.VacationRequests.SingleAsync(x => x.Id == rejectedId)).Status);
        Assert.Equal(8, await verify.NotificationOutbox.CountAsync(x => x.RequestId == requestId));
        Assert.Equal(3, await verify.NotificationOutbox.CountAsync(x => x.RequestId == rejectedId));
        var balance = await VacationLedgerQueries.GetEmploymentBalanceAsync(verify, ids.EmploymentId);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(8, balance.Consumed);
        Assert.Equal(16, balance.Available);
        Assert.True(balance.Available >= 0);
    }

    [Fact]
    public async Task HumanResourcesRejectionAfterManagerApprovalReleasesOriginalReservation()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.ApproveAsync(requestId, "Conforme");
        fixture.AuthenticateAs(fixture.HumanResourcesUser, AppRoles.HumanResources);
        await service.RejectAsync(requestId, "Necesidad operativa");

        await using var db = fixture.CreateTenantDb();
        Assert.Equal(VacationRequestStatuses.Rejected, (await db.VacationRequests.SingleAsync(x => x.Id == requestId)).Status);
        Assert.Equal(0, (await VacationLedgerQueries.GetEmploymentBalanceAsync(db, ids.EmploymentId)).Reserved);
        Assert.Equal(2, await db.VacationMovements.CountAsync(x => x.OperationKey == $"release:vacation-request:{requestId:N}"));
    }

    [Fact]
    public async Task TenantAdministratorWithoutOperationalRoleCannotApprove()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));
        await using var db = fixture.CreateTenantDb();
        Assert.Equal(VacationRequestStatuses.Pending, (await db.VacationRequests.SingleAsync(x => x.Id == requestId)).Status);
        Assert.Equal(6, (await VacationLedgerQueries.GetEmploymentBalanceAsync(db, ids.EmploymentId)).Reserved);
    }

    [Fact]
    public async Task ApproverOutsideDepartmentCannotIntervene()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.OtherDepartmentApproverUser, AppRoles.Approver);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));
    }

    [Fact]
    public async Task HumanResourcesOutsideCompanyScopeCannotIntervene()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.ApproveAsync(requestId, string.Empty);
        fixture.AuthenticateAs(fixture.OutOfScopeHumanResourcesUser, AppRoles.HumanResources);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));
    }

    [Fact]
    public async Task EmployeeCannotApproveOwnRequestEvenWithApproverRole()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee, AppRoles.Approver);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));
        Assert.Contains("propia", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigurationChangesOnlyAffectNewRequests()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var workflow = fixture.Scope.ServiceProvider.GetRequiredService<VacationWorkflowService>();
        var firstId = Guid.NewGuid();
        await service.CreateSelfAsync(firstId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        var configuration = await workflow.GetConfigurationAsync();
        var departmentId = Assert.Single(configuration.Departments).Id;
        await workflow.SaveDepartmentApproversAsync(departmentId, [fixture.OtherDepartmentApproverUser.Id]);
        await workflow.SaveLevelsAsync(configuration.Levels.Select(x =>
            x with { Name = x.Sequence == 1 ? "Jefatura configurada" : x.Name }).ToList());
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee);
        var secondId = Guid.NewGuid();
        await service.CreateSelfAsync(secondId, Monday.AddDays(14), Monday.AddDays(21));

        await using var verify = fixture.CreateTenantDb();
        var firstActors = await verify.VacationApprovalStepActors.Where(x => x.VacationApprovalStep.VacationRequestId == firstId && x.VacationApprovalStep.Type == VacationApprovalLevelTypes.Approver).Select(x => x.UserId).ToListAsync();
        var secondActors = await verify.VacationApprovalStepActors.Where(x => x.VacationApprovalStep.VacationRequestId == secondId && x.VacationApprovalStep.Type == VacationApprovalLevelTypes.Approver).Select(x => x.UserId).ToListAsync();
        Assert.Contains(fixture.ApproverUser.Id, firstActors);
        Assert.DoesNotContain(fixture.OtherDepartmentApproverUser.Id, firstActors);
        Assert.Contains(fixture.OtherDepartmentApproverUser.Id, secondActors);
        Assert.Equal("Jefe / Autorizador", await verify.VacationApprovalSteps.Where(x => x.VacationRequestId == firstId && x.Sequence == 1).Select(x => x.Name).SingleAsync());
        Assert.Equal("Jefatura configurada", await verify.VacationApprovalSteps.Where(x => x.VacationRequestId == secondId && x.Sequence == 1).Select(x => x.Name).SingleAsync());
    }

    [Fact]
    public async Task OneOfSeveralApproversIsSufficientWithoutContradictoryDecision()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.SecondApproverUser, AppRoles.Approver);
        await service.ApproveAsync(requestId, "Aprobación suficiente");
        await service.ApproveAsync(requestId, "Reintento");
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RejectAsync(requestId, "Contradicción"));

        await using var db = fixture.CreateTenantDb();
        Assert.Single(await db.VacationApprovalSteps.Where(x => x.VacationRequestId == requestId && x.Status == VacationApprovalStepStatuses.Approved).ToListAsync());
        Assert.Equal(6, (await VacationLedgerQueries.GetEmploymentBalanceAsync(db, ids.EmploymentId)).Reserved);
        Assert.Equal(0, await db.VacationMovements.CountAsync(x => x.OperationKey == $"consume:vacation-request:{requestId:N}"));
    }

    [Fact]
    public async Task RequestIsRejectedClearlyWhenDepartmentHasNoConfiguredApprover()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var workflow = fixture.Scope.ServiceProvider.GetRequiredService<VacationWorkflowService>();
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        var departmentId = Assert.Single((await workflow.GetConfigurationAsync()).Departments).Id;
        await workflow.SaveDepartmentApproversAsync(departmentId, []);
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSelfAsync(Guid.NewGuid(), Monday, Monday.AddDays(7)));
        Assert.Contains("configure el circuito", exception.Message, StringComparison.OrdinalIgnoreCase);
        await using var db = fixture.CreateTenantDb();
        Assert.Empty(await db.VacationRequests.ToListAsync());
        Assert.Equal(0, (await VacationLedgerQueries.GetEmploymentBalanceAsync(db, ids.EmploymentId)).Reserved);
    }

    [Fact]
    public async Task TenantCancellationSettingIsEnforcedByBackend()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var workflow = fixture.Scope.ServiceProvider.GetRequiredService<VacationWorkflowService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        await workflow.SaveCancellationSettingAsync(false);
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelSelfAsync(requestId));
        await using var db = fixture.CreateTenantDb();
        Assert.Equal(6, (await VacationLedgerQueries.GetEmploymentBalanceAsync(db, ids.EmploymentId)).Reserved);
    }

    [Fact]
    public async Task LegacyPendingRequestIsRegularizedWithoutChangingReservation()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var requests = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        var workflow = fixture.Scope.ServiceProvider.GetRequiredService<VacationWorkflowService>();
        var requestId = Guid.NewGuid();
        await requests.CreateSelfAsync(requestId, Monday, Monday.AddDays(7));
        await using (var db = fixture.CreateTenantDb())
        {
            db.VacationApprovalStepActors.RemoveRange(await db.VacationApprovalStepActors.Where(x => x.VacationApprovalStep.VacationRequestId == requestId).ToListAsync());
            await db.SaveChangesAsync();
            db.VacationApprovalSteps.RemoveRange(await db.VacationApprovalSteps.Where(x => x.VacationRequestId == requestId).ToListAsync());
            await db.SaveChangesAsync();
        }
        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        Assert.Equal(1, await workflow.RegularizePendingRequestsAsync());

        await using var verify = fixture.CreateTenantDb();
        Assert.Equal(2, await verify.VacationApprovalSteps.CountAsync(x => x.VacationRequestId == requestId));
        Assert.Equal(6, (await VacationLedgerQueries.GetEmploymentBalanceAsync(verify, ids.EmploymentId)).Reserved);
        Assert.Equal(2, await verify.VacationMovements.CountAsync(x => x.OperationKey == $"vacation-request:{requestId:N}"));
    }

    [Fact]
    public async Task PermissionDaysAndHoursUseScheduleHolidayAndBreakSnapshots()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        var ids = await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();

        var days = await service.PreviewSelfAsync(PermissionRequestUnits.Days, Monday, Sunday, null, null);
        Assert.Equal(5, days.RequestedDays);
        Assert.Equal(5, days.Dates.Count(x => x.CountsAsPermissionDay));

        var hours = await service.PreviewSelfAsync(PermissionRequestUnits.Hours, Monday, Monday, new(11, 0), new(14, 0));
        Assert.Equal(120, hours.RequestedMinutes);
        Assert.Equal(new TimeOnly(12, 0), Assert.Single(hours.Dates).BreakStart);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreviewSelfAsync(PermissionRequestUnits.Hours, Monday, Monday, new(7, 0), new(9, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreviewSelfAsync(PermissionRequestUnits.Hours, Monday, Monday, new(12, 0), new(13, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreviewSelfAsync(PermissionRequestUnits.Hours, Monday, Monday, new(10, 0), new(9, 0)));

        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, PermissionRequestTypes.Unpaid, PermissionRequestUnits.Days,
            Monday.AddDays(14), Monday.AddDays(18), null, null, "Trámite personal");
        await using var db = fixture.CreateTenantDb();
        var request = await db.PermissionRequests.Include(x => x.Dates).SingleAsync(x => x.Id == requestId);
        Assert.Equal(PermissionRequestTypes.Unpaid, request.PermissionType);
        Assert.Equal(5, request.RequestedDays);
        Assert.All(request.Dates, x => Assert.Equal("Lunes a viernes", x.WorkScheduleName));
        Assert.Equal(3, await db.VacationMovements.CountAsync());
    }

    [Fact]
    public async Task PermissionWorkflowEnforcesRolesScopesIdempotencyAndCreatesNotifications()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, PermissionRequestTypes.Paid, PermissionRequestUnits.Hours,
            Monday, Monday, new(9, 0), new(11, 0), "Consulta médica");

        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));
        fixture.AuthenticateAs(fixture.OtherDepartmentApproverUser, AppRoles.Approver);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee, AppRoles.Approver);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));

        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.ApproveAsync(requestId, "Conforme");
        await service.ApproveAsync(requestId, "Reintento");
        fixture.AuthenticateAs(fixture.OutOfScopeHumanResourcesUser, AppRoles.HumanResources);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, string.Empty));
        fixture.AuthenticateAs(fixture.HumanResourcesUser, AppRoles.HumanResources);
        await service.ApproveAsync(requestId, "Aprobación final");
        await service.ApproveAsync(requestId, "Reintento");

        await using var db = fixture.CreateTenantDb();
        Assert.Equal(PermissionRequestStatuses.Approved, (await db.PermissionRequests.SingleAsync(x => x.Id == requestId)).Status);
        Assert.Equal(2, await db.PermissionApprovalSteps.CountAsync(x => x.PermissionRequestId == requestId && x.Status == VacationApprovalStepStatuses.Approved));
        Assert.Equal(4, await db.NotificationOutbox.CountAsync(x => x.RequestId == requestId));
        Assert.Equal(3, await db.VacationMovements.CountAsync());
    }

    [Fact]
    public async Task PermissionOverlapRejectsPermissionsAndVacationsInBothDirections()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var permissions = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var vacations = fixture.Scope.ServiceProvider.GetRequiredService<VacationRequestService>();
        await permissions.CreateSelfAsync(Guid.NewGuid(), PermissionRequestTypes.Paid, PermissionRequestUnits.Days,
            Monday, Monday, null, null, "Ausencia completa");
        await Assert.ThrowsAsync<InvalidOperationException>(() => permissions.CreateSelfAsync(Guid.NewGuid(),
            PermissionRequestTypes.Paid, PermissionRequestUnits.Hours, Monday, Monday, new(10, 0), new(11, 0), "Solapado"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => vacations.CreateSelfAsync(Guid.NewGuid(), Monday, Monday.AddDays(1)));

        await vacations.CreateSelfAsync(Guid.NewGuid(), Monday.AddDays(14), Monday.AddDays(18));
        await Assert.ThrowsAsync<InvalidOperationException>(() => permissions.CreateSelfAsync(Guid.NewGuid(),
            PermissionRequestTypes.Unpaid, PermissionRequestUnits.Days, Monday.AddDays(15), Monday.AddDays(16), null, null, "Solapado con vacaciones"));
    }

    [Fact]
    public async Task PermissionCancellationPreservesDecisionsAndHonorsTenantSetting()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var workflow = fixture.Scope.ServiceProvider.GetRequiredService<VacationWorkflowService>();

        var pendingId = Guid.NewGuid();
        await service.CreateSelfAsync(pendingId, PermissionRequestTypes.Paid, PermissionRequestUnits.Days,
            Monday, Monday, null, null, "Pendiente");
        await service.CancelSelfAsync(pendingId, "Ya no es necesario");

        var approvedId = Guid.NewGuid();
        await service.CreateSelfAsync(approvedId, PermissionRequestTypes.Unpaid, PermissionRequestUnits.Hours,
            Monday.AddDays(1), Monday.AddDays(1), new(9, 0), new(10, 0), "Aprobado");
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.ApproveAsync(approvedId, "Jefatura");
        fixture.AuthenticateAs(fixture.HumanResourcesUser, AppRoles.HumanResources);
        await service.ApproveAsync(approvedId, "RR. HH.");
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee);
        await service.CancelSelfAsync(approvedId, "Cambio de planes");

        fixture.AuthenticateAs(fixture.AdministratorUser, AppRoles.TenantAdministrator);
        await workflow.SaveCancellationSettingAsync(false);
        fixture.AuthenticateAs(fixture.User, AppRoles.Employee);
        var blockedId = Guid.NewGuid();
        await service.CreateSelfAsync(blockedId, PermissionRequestTypes.Paid, PermissionRequestUnits.Hours,
            Monday.AddDays(2), Monday.AddDays(2), new(9, 0), new(10, 0), "No cancelable");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelSelfAsync(blockedId, string.Empty));

        await using var db = fixture.CreateTenantDb();
        Assert.Equal(PermissionRequestStatuses.Cancelled, (await db.PermissionRequests.SingleAsync(x => x.Id == pendingId)).Status);
        Assert.Equal(PermissionRequestStatuses.Cancelled, (await db.PermissionRequests.SingleAsync(x => x.Id == approvedId)).Status);
        Assert.Equal(2, await db.PermissionApprovalSteps.CountAsync(x => x.PermissionRequestId == approvedId && x.Status == VacationApprovalStepStatuses.Approved));
        Assert.Equal(PermissionRequestStatuses.Pending, (await db.PermissionRequests.SingleAsync(x => x.Id == blockedId)).Status);
        Assert.Equal(4, await db.NotificationOutbox.CountAsync(x => x.RequestId == approvedId && x.EventType == "CANCELLED"));
    }

    [Fact]
    public async Task PermissionRejectionsAtEitherLevelAreFinalAndNotifyEmployee()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();

        var managerRejectedId = Guid.NewGuid();
        await service.CreateSelfAsync(managerRejectedId, PermissionRequestTypes.Paid, PermissionRequestUnits.Hours,
            Monday, Monday, new(9, 0), new(10, 0), "Primera solicitud");
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.RejectAsync(managerRejectedId, "Rechazo de jefatura");

        fixture.AuthenticateAs(fixture.User, AppRoles.Employee);
        var hrRejectedId = Guid.NewGuid();
        await service.CreateSelfAsync(hrRejectedId, PermissionRequestTypes.Unpaid, PermissionRequestUnits.Hours,
            Monday.AddDays(1), Monday.AddDays(1), new(9, 0), new(10, 0), "Segunda solicitud");
        fixture.AuthenticateAs(fixture.ApproverUser, AppRoles.Approver);
        await service.ApproveAsync(hrRejectedId, "Conforme");
        fixture.AuthenticateAs(fixture.HumanResourcesUser, AppRoles.HumanResources);
        await service.RejectAsync(hrRejectedId, "Rechazo de RR. HH.");

        await using var db = fixture.CreateTenantDb();
        Assert.Equal(PermissionRequestStatuses.Rejected, (await db.PermissionRequests.SingleAsync(x => x.Id == managerRejectedId)).Status);
        Assert.Equal(PermissionRequestStatuses.Rejected, (await db.PermissionRequests.SingleAsync(x => x.Id == hrRejectedId)).Status);
        Assert.Equal(1, await db.NotificationOutbox.CountAsync(x => x.RequestId == managerRejectedId && x.EventType == "REJECTED" && x.RecipientUserId == fixture.User.Id));
        Assert.Equal(1, await db.NotificationOutbox.CountAsync(x => x.RequestId == hrRejectedId && x.EventType == "REJECTED" && x.RecipientUserId == fixture.User.Id));
    }

    [Fact]
    public async Task OutboxFailureDoesNotLoseRequestAndRetryDoesNotDuplicateEvents()
    {
        await using var fixture = await RequestServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var service = fixture.Scope.ServiceProvider.GetRequiredService<PermissionRequestService>();
        var worker = fixture.Scope.ServiceProvider.GetRequiredService<NotificationOutboxHostedService>();
        var transport = fixture.Scope.ServiceProvider.GetRequiredService<FailingNotificationTransport>();
        var requestId = Guid.NewGuid();
        await service.CreateSelfAsync(requestId, PermissionRequestTypes.Paid, PermissionRequestUnits.Hours,
            Monday, Monday, new(9, 0), new(10, 0), "Notificación");

        await worker.ProcessTenantOnceAsync(fixture.Tenant.Id, fixture.SchemaName);
        await using (var failed = fixture.CreateTenantDb())
        {
            Assert.Equal(PermissionRequestStatuses.Pending, (await failed.PermissionRequests.SingleAsync(x => x.Id == requestId)).Status);
            Assert.Equal(2, await failed.NotificationOutbox.CountAsync(x => x.RequestId == requestId));
            Assert.All(await failed.NotificationOutbox.Where(x => x.RequestId == requestId).ToListAsync(), x =>
            {
                Assert.Equal(NotificationOutboxStatuses.Pending, x.Status);
                Assert.Equal(1, x.AttemptCount);
                Assert.Contains("SMTP", x.LastError);
                x.NextAttemptAtUtc = null;
            });
            await failed.SaveChangesAsync();
        }

        transport.Fail = false;
        await worker.ProcessTenantOnceAsync(fixture.Tenant.Id, fixture.SchemaName);
        await using var sent = fixture.CreateTenantDb();
        Assert.Equal(2, await sent.NotificationOutbox.CountAsync(x => x.RequestId == requestId));
        Assert.Equal(2, await sent.NotificationOutbox.CountAsync(x => x.RequestId == requestId && x.Status == NotificationOutboxStatuses.Sent));
        Assert.Equal(2, transport.Recipients.Count);
    }

    private static string Connection() => Environment.GetEnvironmentVariable("HRPROTOOLS_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Defina HRPROTOOLS_TEST_CONNECTION.");

    private static string Schema() => "hr_t_" + Guid.NewGuid().ToString("N");

    private sealed class TenantSchemaFixture : IAsyncDisposable
    {
        public required string SchemaName { get; init; }

        public static async Task<TenantSchemaFixture> CreateAsync()
        {
            var schema = Schema();
            await using var admin = new NpgsqlConnection(Connection());
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\"");
            var fixture = new TenantSchemaFixture { SchemaName = schema };
            await using var db = fixture.CreateDb();
            await db.Database.MigrateAsync();
            return fixture;
        }

        public TenantDbContext CreateDb()
        {
            var cs = new NpgsqlConnectionStringBuilder(Connection()) { SearchPath = SchemaName };
            return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(cs.ConnectionString).Options);
        }

        public async Task<(Guid EmploymentId, Guid CompanyId)> SeedEmploymentAsync(
            bool saturday,
            bool sunday,
            DateOnly? scheduleStart = null,
            DateOnly? scheduleEnd = null)
        {
            await using var db = CreateDb();
            var company = new Company { Code = "EMP", LegalName = "Empresa", TradeName = "Empresa", TaxId = Guid.NewGuid().ToString("N") };
            var person = new Person { DocumentNumber = "1", NormalizedDocumentNumber = "1", GivenNames = "Ana", FamilyNames = "Prueba" };
            var employment = new Employment { EmployeeNumber = "1", Person = person, Company = company, StartDate = new(2020, 1, 1) };
            var schedule = new WorkSchedule
            {
                Name = "Jornada de prueba",
                StartTime = new(8, 0),
                EndTime = new(17, 0),
                Saturday = saturday,
                Sunday = sunday
            };
            db.AddRange(company, person, employment, schedule);
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule
            {
                Employment = employment,
                WorkSchedule = schedule,
                ValidFrom = scheduleStart ?? employment.StartDate,
                ValidTo = scheduleEnd
            });
            await db.SaveChangesAsync();
            return (employment.Id, company.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await using var admin = new NpgsqlConnection(Connection());
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{SchemaName}\" CASCADE");
        }
    }

    private sealed class RequestServiceFixture : IAsyncDisposable
    {
        public required string SchemaName { get; init; }
        public required Tenant Tenant { get; init; }
        public required ApplicationUser User { get; init; }
        public required ApplicationUser ApproverUser { get; init; }
        public required ApplicationUser SecondApproverUser { get; init; }
        public required ApplicationUser OtherDepartmentApproverUser { get; init; }
        public required ApplicationUser HumanResourcesUser { get; init; }
        public required ApplicationUser OutOfScopeHumanResourcesUser { get; init; }
        public required ApplicationUser AdministratorUser { get; init; }
        public required MutableAuthenticationStateProvider Authentication { get; init; }
        public required ServiceProvider Provider { get; init; }
        public required AsyncServiceScope Scope { get; init; }

        public static async Task<RequestServiceFixture> CreateAsync()
        {
            var schema = Schema();
            await using (var admin = new NpgsqlConnection(Connection()))
            {
                await admin.OpenAsync();
                await ExecuteAsync(admin, $"CREATE SCHEMA \"{schema}\"");
            }
            var tenant = new Tenant { Code = $"T{Guid.NewGuid():N}"[..12], Name = "Tenant fase 4", SchemaName = schema, Status = TenantStatuses.Active };
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = $"phase4-{Guid.NewGuid():N}@test.local",
                NormalizedUserName = $"PHASE4-{Guid.NewGuid():N}@TEST.LOCAL",
                Email = $"phase4-{Guid.NewGuid():N}@test.local",
                NormalizedEmail = $"PHASE4-{Guid.NewGuid():N}@TEST.LOCAL",
                DisplayName = "Prueba Fase 4",
                TenantId = tenant.Id,
                IsActive = true
            };
            var approver = TestUser(tenant.Id, "approver", "Jefe Prueba");
            var secondApprover = TestUser(tenant.Id, "approver2", "Segunda Jefa");
            var otherDepartmentApprover = TestUser(tenant.Id, "approver-other", "Jefe Otro Departamento");
            var humanResources = TestUser(tenant.Id, "hr", "RR. HH. Prueba");
            var outOfScopeHumanResources = TestUser(tenant.Id, "hr-other", "RR. HH. Otra Empresa");
            var administrator = TestUser(tenant.Id, "admin", "Administrador Tenant");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = Connection() }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                Connection(), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "auth")));
            var authentication = new MutableAuthenticationStateProvider();
            authentication.Set(user.Id, AppRoles.Employee);
            services.AddSingleton<AuthenticationStateProvider>(authentication);
            services.AddAuthorizationBuilder()
                .AddPolicy(AppPolicies.EmployeeSelfService, policy => policy.RequireRole(AppRoles.Employee))
                .AddPolicy(AppPolicies.TenantAdministration, policy => policy.RequireRole(AppRoles.TenantAdministrator))
                .AddPolicy(AppPolicies.VacationWorkflowAccess, policy => policy.RequireRole(AppRoles.TenantAdministrator, AppRoles.Approver, AppRoles.HumanResources));
            services.AddScoped<CurrentTenantService>();
            services.AddSingleton<TenantDbContextFactory>();
            services.AddScoped<VacationWorkflowService>();
            services.AddScoped<VacationRequestService>();
            services.AddScoped<PermissionRequestService>();
            services.AddScoped<NotificationOutboxService>();
            services.AddSingleton<FailingNotificationTransport>();
            services.AddSingleton<INotificationEmailTransport>(sp => sp.GetRequiredService<FailingNotificationTransport>());
            services.AddSingleton<NotificationOutboxHostedService>();
            services.AddSingleton<NotificationOutboxHostedService>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var global = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await global.Database.MigrateAsync();
            global.Tenants.Add(tenant);
            await EnsureRolesAsync(global);
            global.Users.AddRange(user, approver, secondApprover, otherDepartmentApprover, humanResources, outOfScopeHumanResources, administrator);
            global.UserRoles.AddRange(
                new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = user.Id, RoleId = await RoleIdAsync(global, AppRoles.Employee) },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = approver.Id, RoleId = await RoleIdAsync(global, AppRoles.Approver) },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = secondApprover.Id, RoleId = await RoleIdAsync(global, AppRoles.Approver) },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = otherDepartmentApprover.Id, RoleId = await RoleIdAsync(global, AppRoles.Approver) },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = humanResources.Id, RoleId = await RoleIdAsync(global, AppRoles.HumanResources) },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = outOfScopeHumanResources.Id, RoleId = await RoleIdAsync(global, AppRoles.HumanResources) },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = administrator.Id, RoleId = await RoleIdAsync(global, AppRoles.TenantAdministrator) });
            await global.SaveChangesAsync();
            await using (var tenantDb = CreateTenantDb(schema)) await tenantDb.Database.MigrateAsync();
            return new RequestServiceFixture { SchemaName = schema, Tenant = tenant, User = user, ApproverUser = approver, SecondApproverUser = secondApprover, OtherDepartmentApproverUser = otherDepartmentApprover, HumanResourcesUser = humanResources, OutOfScopeHumanResourcesUser = outOfScopeHumanResources, AdministratorUser = administrator, Authentication = authentication, Provider = provider, Scope = scope };
        }

        public void AuthenticateAs(ApplicationUser user, params string[] roles) => Authentication.Set(user.Id, roles);

        public TenantDbContext CreateTenantDb() => CreateTenantDb(SchemaName);

        public async Task<(Guid EmploymentId, Guid CompanyId, Guid FirstPeriodId, Guid SecondPeriodId)> SeedAsync()
        {
            await using var db = CreateTenantDb();
            var company = new Company { Code = "EMP", LegalName = "Empresa", TradeName = "Empresa", TaxId = Guid.NewGuid().ToString("N") };
            var otherCompany = new Company { Code = "EMP2", LegalName = "Otra empresa", TradeName = "Otra empresa", TaxId = Guid.NewGuid().ToString("N") };
            var person = new Person { DocumentNumber = "99", NormalizedDocumentNumber = "99", GivenNames = "FIFO", FamilyNames = "Prueba" };
            var employment = new Employment { EmployeeNumber = "99", Person = person, Company = company, StartDate = new(2020, 1, 1) };
            var department = new Department { Company = company, Code = "DEP", Name = "Operaciones" };
            var position = new Position { Company = company, Code = "POS", Name = "Analista" };
            var schedule = new WorkSchedule { Name = "Lunes a viernes", StartTime = new(8, 0), EndTime = new(17, 0), BreakStart = new(12, 0), BreakEnd = new(13, 0) };
            var first = new VacationPeriod { Employment = employment, ReferenceStart = new(2024, 1, 1), ReferenceEnd = new(2024, 12, 31), AccruedOn = new(2025, 1, 1), UseBy = new(2025, 7, 1), LegalDays = 12 };
            var second = new VacationPeriod { Employment = employment, ReferenceStart = new(2025, 1, 1), ReferenceEnd = new(2025, 12, 31), AccruedOn = new(2026, 1, 1), UseBy = new(2026, 7, 1), LegalDays = 12 };
            db.AddRange(company, otherCompany, department, position, person, employment, schedule, first, second);
            db.EmploymentAssignments.Add(new EmploymentAssignment { Employment = employment, Department = department, Position = position, ValidFrom = employment.StartDate });
            db.EmploymentWorkSchedules.Add(new EmploymentWorkSchedule { Employment = employment, WorkSchedule = schedule, ValidFrom = employment.StartDate });
            db.DepartmentApproverAssignments.Add(new DepartmentApproverAssignment { Department = department, UserId = ApproverUser.Id });
            db.DepartmentApproverAssignments.Add(new DepartmentApproverAssignment { Department = department, UserId = SecondApproverUser.Id });
            db.HumanResourcesScopes.Add(new HumanResourcesScope { UserId = HumanResourcesUser.Id, Company = company });
            db.HumanResourcesScopes.Add(new HumanResourcesScope { UserId = OutOfScopeHumanResourcesUser.Id, Company = otherCompany });
            db.VacationMovements.AddRange(
                new VacationMovement { VacationPeriod = first, Type = VacationMovementTypes.Credit, LegalDays = 12, OperationKey = "phase4-credit-1" },
                new VacationMovement { VacationPeriod = first, Type = VacationMovementTypes.Consume, LegalDays = 8, OperationKey = "phase4-used-1" },
                new VacationMovement { VacationPeriod = second, Type = VacationMovementTypes.Credit, LegalDays = 12, OperationKey = "phase4-credit-2" });
            await db.SaveChangesAsync();

            var global = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await global.Users.SingleAsync(x => x.Id == User.Id);
            user.PersonId = person.Id;
            user.ActiveEmploymentId = employment.Id;
            await global.SaveChangesAsync();
            return (employment.Id, company.Id, first.Id, second.Id);
        }

        public async ValueTask DisposeAsync()
        {
            var global = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            global.Users.RemoveRange(User, ApproverUser, SecondApproverUser, OtherDepartmentApproverUser, HumanResourcesUser, OutOfScopeHumanResourcesUser, AdministratorUser);
            global.Tenants.Remove(Tenant);
            await global.SaveChangesAsync();
            await Scope.DisposeAsync();
            await Provider.DisposeAsync();
            await using var admin = new NpgsqlConnection(Connection());
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"DROP SCHEMA IF EXISTS \"{SchemaName}\" CASCADE");
        }

        private static TenantDbContext CreateTenantDb(string schema)
        {
            var cs = new NpgsqlConnectionStringBuilder(Connection()) { SearchPath = schema };
            return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(cs.ConnectionString).Options);
        }

        private static ApplicationUser TestUser(Guid tenantId, string prefix, string name)
        {
            var email = $"{prefix}-{Guid.NewGuid():N}@test.local";
            return new ApplicationUser { Id = Guid.NewGuid().ToString("N"), UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email, NormalizedEmail = email.ToUpperInvariant(), DisplayName = name, TenantId = tenantId, IsActive = true };
        }

        private static async Task EnsureRolesAsync(ApplicationDbContext db)
        {
            foreach (var role in new[] { AppRoles.Employee, AppRoles.Approver, AppRoles.HumanResources, AppRoles.TenantAdministrator })
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.\"AspNetRoles\" (\"Id\", \"Name\", \"NormalizedName\", \"ConcurrencyStamp\") VALUES ({Guid.NewGuid().ToString("N")}, {role}, {role.ToUpperInvariant()}, {Guid.NewGuid().ToString("N")}) ON CONFLICT (\"NormalizedName\") DO NOTHING");
        }

        private static Task<string> RoleIdAsync(ApplicationDbContext db, string role) =>
            db.Roles.Where(x => x.NormalizedName == role.ToUpperInvariant()).Select(x => x.Id).SingleAsync();
    }

    private sealed class MutableAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState state = new(new ClaimsPrincipal(new ClaimsIdentity()));
        public void Set(string userId, params string[] roles) => state = new(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId) }.Concat(roles.Select(x => new Claim(ClaimTypes.Role, x))), "Test")));
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(state);
    }

    private sealed class FailingNotificationTransport : INotificationEmailTransport
    {
        public bool Fail { get; set; } = true;
        public List<string> Recipients { get; } = [];
        public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("SMTP de prueba no disponible.");
            Recipients.Add(recipient);
            return Task.CompletedTask;
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
