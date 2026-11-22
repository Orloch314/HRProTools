using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record DashboardMetric(string Label, int Value, string? Link = null);
public sealed record RoleDashboard(string Title, string Subtitle, IReadOnlyList<DashboardMetric> Metrics, IReadOnlyList<OperationalAlertView> Alerts);

public sealed class RoleDashboardService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory tenantFactory,
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopes,
    OperationalAlertService alertService)
{
    public async Task<RoleDashboard> GetAsync(CancellationToken ct = default)
    {
        var principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var tenant = await currentTenant.GetRequiredAsync(ct);
        await using var db = tenantFactory.Create(tenant.SchemaName);

        if (principal.IsInRole(AppRoles.HumanResources))
        {
            var companies = await OperationalAlertService.HumanResourcesCompanyIdsAsync(db, userId, ct);
            var employeeCount = await db.Employments.CountAsync(x => x.Status == EmploymentStatuses.Active && companies.Contains(x.CompanyId), ct);
            var pendingRequests = await db.VacationApprovalSteps.CountAsync(x => x.Status == VacationApprovalStepStatuses.Pending && x.Type == VacationApprovalLevelTypes.HumanResources && x.Actors.Any(a => a.UserId == userId), ct)
                + await db.PermissionApprovalSteps.CountAsync(x => x.Status == VacationApprovalStepStatuses.Pending && x.Type == VacationApprovalLevelTypes.HumanResources && x.Actors.Any(a => a.UserId == userId), ct);
            var pendingAbsences = await db.Absences.CountAsync(x => companies.Contains(x.CompanyId) && (x.Status == AbsenceStatuses.Declared || x.Status == AbsenceStatuses.PendingReview), ct);
            var today = ParaguayVacationRules.ParaguayToday();
            var pendingVacationPeriods = await db.VacationPeriods.Where(x => companies.Contains(x.Employment.CompanyId) && x.UseBy < today)
                .CountAsync(x => x.LegalDays - db.VacationMovements.Where(m => m.VacationPeriodId == x.Id && m.Type == VacationMovementTypes.Consume).Sum(m => m.LegalDays)
                    + db.VacationMovements.Where(m => m.VacationPeriodId == x.Id && m.Type == VacationMovementTypes.Reversal).Sum(m => m.LegalDays) > 0, ct);
            var alerts = await alertService.GetHumanResourcesAlertsAsync(ct);
            return new("Dashboard RR. HH.", "Solicitudes, ausencias, vacaciones y alertas dentro de su alcance empresarial.",
                [new("Funcionarios activos", employeeCount, "/tenant/funcionarios"), new("Solicitudes pendientes", pendingRequests, "/tenant/solicitudes"), new("Ausencias por revisar", pendingAbsences, "/tenant/ausencias"), new("Periodos pendientes", pendingVacationPeriods, "/tenant/funcionarios"), new("Alertas preventivas", alerts.Count, "/dashboard")], alerts);
        }

        if (principal.IsInRole(AppRoles.Approver))
        {
            var departments = (await db.DepartmentApproverAssignments.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.DepartmentId).ToListAsync(ct)).ToHashSet();
            var employees = await db.EmploymentAssignments.CountAsync(x => x.ValidTo == null && departments.Contains(x.DepartmentId) && x.Employment.Status == EmploymentStatuses.Active, ct);
            var pending = await db.VacationApprovalSteps.CountAsync(x => x.Status == VacationApprovalStepStatuses.Pending && x.Type == VacationApprovalLevelTypes.Approver && x.Actors.Any(a => a.UserId == userId), ct)
                + await db.PermissionApprovalSteps.CountAsync(x => x.Status == VacationApprovalStepStatuses.Pending && x.Type == VacationApprovalLevelTypes.Approver && x.Actors.Any(a => a.UserId == userId), ct);
            var absences = await db.Absences.CountAsync(x => departments.Contains(x.DepartmentId) && x.Status != AbsenceStatuses.Cancelled, ct);
            return new("Panel del autorizador", "Pendientes y funcionarios de sus departamentos asignados.",
                [new("Solicitudes pendientes", pending, "/tenant/solicitudes"), new("Funcionarios autorizados", employees), new("Ausencias visibles", absences, "/tenant/ausencias")], []);
        }

        if (principal.IsInRole(AppRoles.Employee))
        {
            await using var globalScope = scopes.CreateAsyncScope();
            var global = globalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employmentId = await global.Users.AsNoTracking().Where(x => x.Id == userId && x.TenantId == tenant.Id).Select(x => x.ActiveEmploymentId).SingleAsync(ct)
                ?? throw new UnauthorizedAccessException();
            var balance = await VacationLedgerQueries.GetEmploymentBalanceAsync(db, employmentId, ct);
            return new("Mi panel", "Resumen personal de vacaciones, solicitudes y ausencias.",
                [new("Dias disponibles", (int)Math.Floor(balance.Available), "/empleado/vacaciones"), new("Vacaciones solicitadas", await db.VacationRequests.CountAsync(x => x.EmploymentId == employmentId, ct), "/empleado/vacaciones"), new("Permisos solicitados", await db.PermissionRequests.CountAsync(x => x.EmploymentId == employmentId, ct), "/empleado/permisos"), new("Ausencias registradas", await db.Absences.CountAsync(x => x.EmploymentId == employmentId, ct), "/empleado/ausencias")], []);
        }

        if (principal.IsInRole(AppRoles.TenantAdministrator))
        {
            await using var globalScope = scopes.CreateAsyncScope();
            var global = globalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = await global.Users.AsNoTracking().Where(x => x.TenantId == tenant.Id).ToListAsync(ct);
            return new("Administracion del grupo", "Estado administrativo sin informacion salarial ni medica.",
                [new("Empresas activas", await db.Companies.CountAsync(x => x.IsActive, ct), "/tenant/organizacion"), new("Funcionarios activos", await db.Employments.CountAsync(x => x.Status == EmploymentStatuses.Active, ct), "/tenant/funcionarios"), new("Usuarios activos", users.Count(x => x.IsActive)), new("Invitaciones pendientes", users.Count(x => x.InvitationSentAtUtc != null && x.InvitationAcceptedAtUtc == null))], []);
        }
        throw new UnauthorizedAccessException("No existe un dashboard para este rol.");
    }
}
