using System.Data;
using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record VacationApprovalLevelInput(Guid? Id, int Sequence, string Name, string Type);
public sealed record VacationWorkflowUser(string Id, string DisplayName, string Email);
public sealed record VacationWorkflowDepartment(Guid Id, Guid CompanyId, string Company, string Name);
public sealed record VacationWorkflowCompany(Guid Id, string Name);
public sealed record DepartmentApproverValue(Guid DepartmentId, string UserId);
public sealed record HumanResourcesScopeValue(string UserId, Guid? CompanyId);
public sealed record ApprovalStepSnapshot(
    int Sequence,
    string Name,
    string Type,
    string Status,
    Guid DepartmentId,
    Guid CompanyId,
    IReadOnlyList<string> ActorUserIds);
public sealed record RequestSelfLink(string UserId, Guid TenantId, string SchemaName, Guid EmploymentId);
public sealed record RequestWorkflowActor(
    string UserId,
    Guid TenantId,
    string SchemaName,
    bool IsAdministrator,
    bool IsApprover,
    bool IsHumanResources);
public sealed record VacationWorkflowConfiguration(
    IReadOnlyList<VacationApprovalLevelInput> Levels,
    IReadOnlyList<VacationWorkflowUser> Approvers,
    IReadOnlyList<VacationWorkflowUser> HumanResourcesUsers,
    IReadOnlyList<VacationWorkflowDepartment> Departments,
    IReadOnlyList<VacationWorkflowCompany> Companies,
    IReadOnlyList<DepartmentApproverValue> DepartmentApprovers,
    IReadOnlyList<HumanResourcesScopeValue> HumanResourcesScopes,
    bool AllowEmployeeCancellation,
    int PendingRequestsWithoutCircuit);

public sealed class VacationWorkflowService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory tenantFactory,
    IServiceScopeFactory scopeFactory,
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorizationService)
{
    public async Task<VacationWorkflowConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var actor = await GetAdministratorAsync(cancellationToken);
        await using var db = tenantFactory.Create(actor.Tenant.SchemaName);
        var users = await GetOperationalUsersAsync(actor.Tenant.Id, cancellationToken);
        var settings = await db.VacationWorkflowSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return new(
            await db.VacationApprovalLevels.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Sequence)
                .Select(x => new VacationApprovalLevelInput(x.Id, x.Sequence, x.Name, x.Type)).ToListAsync(cancellationToken),
            users.Where(x => x.Roles.Contains(AppRoles.Approver)).Select(x => x.User).ToList(),
            users.Where(x => x.Roles.Contains(AppRoles.HumanResources)).Select(x => x.User).ToList(),
            await db.Departments.AsNoTracking().Where(x => x.IsActive && x.Company.IsActive).OrderBy(x => x.Company.LegalName).ThenBy(x => x.Name)
                .Select(x => new VacationWorkflowDepartment(x.Id, x.CompanyId, x.Company.LegalName, x.Name)).ToListAsync(cancellationToken),
            await db.Companies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.LegalName)
                .Select(x => new VacationWorkflowCompany(x.Id, x.LegalName)).ToListAsync(cancellationToken),
            await db.DepartmentApproverAssignments.AsNoTracking()
                .Select(x => new DepartmentApproverValue(x.DepartmentId, x.UserId)).ToListAsync(cancellationToken),
            await db.HumanResourcesScopes.AsNoTracking()
                .Select(x => new HumanResourcesScopeValue(x.UserId, x.CompanyId)).ToListAsync(cancellationToken),
            settings?.AllowEmployeeCancellation ?? true,
            await db.VacationRequests.CountAsync(x => x.Status == VacationRequestStatuses.Pending && !x.ApprovalSteps.Any(), cancellationToken));
    }

    public async Task SaveLevelsAsync(IReadOnlyCollection<VacationApprovalLevelInput> values, CancellationToken cancellationToken = default)
    {
        var actor = await GetAdministratorAsync(cancellationToken);
        var normalized = ValidateLevels(values);
        await using var db = tenantFactory.Create(actor.Tenant.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        db.VacationApprovalLevels.RemoveRange(await db.VacationApprovalLevels.ToListAsync(cancellationToken));
        db.VacationApprovalLevels.AddRange(normalized.Select(x => new VacationApprovalLevel
        {
            Id = Guid.NewGuid(),
            Sequence = x.Sequence,
            Name = x.Name,
            Type = x.Type
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveDepartmentApproversAsync(Guid departmentId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var actor = await GetAdministratorAsync(cancellationToken);
        await using var db = tenantFactory.Create(actor.Tenant.SchemaName);
        if (!await db.Departments.AnyAsync(x => x.Id == departmentId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("El departamento no está activo.");
        var valid = await ValidateOperationalUsersAsync(actor.Tenant.Id, userIds, AppRoles.Approver, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        db.DepartmentApproverAssignments.RemoveRange(
            await db.DepartmentApproverAssignments.Where(x => x.DepartmentId == departmentId).ToListAsync(cancellationToken));
        db.DepartmentApproverAssignments.AddRange(valid.Select(userId => new DepartmentApproverAssignment
        {
            DepartmentId = departmentId,
            UserId = userId
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveHumanResourcesScopeAsync(
        string userId,
        bool entireGroup,
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetAdministratorAsync(cancellationToken);
        await ValidateOperationalUsersAsync(actor.Tenant.Id, [userId], AppRoles.HumanResources, cancellationToken);
        await using var db = tenantFactory.Create(actor.Tenant.SchemaName);
        if (!entireGroup)
        {
            if (companyIds.Count == 0)
                throw new InvalidOperationException("Seleccione al menos una empresa o habilite todo el grupo.");
            var validCompanies = await db.Companies.CountAsync(x => companyIds.Contains(x.Id) && x.IsActive, cancellationToken);
            if (validCompanies != companyIds.Distinct().Count())
                throw new InvalidOperationException("Una de las empresas seleccionadas no está activa.");
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        db.HumanResourcesScopes.RemoveRange(await db.HumanResourcesScopes.Where(x => x.UserId == userId).ToListAsync(cancellationToken));
        if (entireGroup)
            db.HumanResourcesScopes.Add(new HumanResourcesScope { UserId = userId });
        else
            db.HumanResourcesScopes.AddRange(companyIds.Distinct().Select(companyId => new HumanResourcesScope { UserId = userId, CompanyId = companyId }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveCancellationSettingAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var actor = await GetAdministratorAsync(cancellationToken);
        await using var db = tenantFactory.Create(actor.Tenant.SchemaName);
        var settings = await db.VacationWorkflowSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
            db.VacationWorkflowSettings.Add(new VacationWorkflowSettings { AllowEmployeeCancellation = enabled });
        else
        {
            settings.AllowEmployeeCancellation = enabled;
            settings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RegularizePendingRequestsAsync(CancellationToken cancellationToken = default)
    {
        var actor = await GetAdministratorAsync(cancellationToken);
        await using var db = tenantFactory.Create(actor.Tenant.SchemaName);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var requests = await db.VacationRequests
            .Where(x => x.Status == VacationRequestStatuses.Pending && !x.ApprovalSteps.Any())
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        foreach (var request in requests)
        {
            var snapshot = await BuildSnapshotAsync(db, actor.Tenant.Id, request.EmploymentId, request.RequestedByUserId, cancellationToken);
            db.VacationApprovalSteps.AddRange(snapshot.Select(step => new VacationApprovalStep
            {
                VacationRequestId = request.Id,
                Sequence = step.Sequence,
                Name = step.Name,
                Type = step.Type,
                Status = step.Status,
                DepartmentId = step.DepartmentId,
                CompanyId = step.CompanyId,
                Actors = step.ActorUserIds.Select(userId => new VacationApprovalStepActor { UserId = userId }).ToList()
            }));
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return requests.Count;
    }

    internal async Task<List<ApprovalStepSnapshot>> BuildSnapshotAsync(
        TenantDbContext db,
        Guid tenantId,
        Guid employmentId,
        string requesterUserId,
        CancellationToken cancellationToken,
        bool legalReviewOnly = false)
    {
        var assignment = await db.EmploymentAssignments.AsNoTracking()
            .Where(x => x.EmploymentId == employmentId && x.ValidTo == null)
            .Select(x => new { x.DepartmentId, x.Department.CompanyId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("El funcionario no tiene un departamento vigente.");
        var levels = await db.VacationApprovalLevels.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        ValidateLevels(levels.Select(x => new VacationApprovalLevelInput(x.Id, x.Sequence, x.Name, x.Type)).ToList());
        var operationalUsers = await GetOperationalUsersAsync(tenantId, cancellationToken);
        var steps = new List<ApprovalStepSnapshot>();
        foreach (var level in levels)
        {
            if (legalReviewOnly && level.Type != VacationApprovalLevelTypes.HumanResources) continue;
            List<string> actorIds;
            if (level.Type == VacationApprovalLevelTypes.Approver)
            {
                var configured = await db.DepartmentApproverAssignments.AsNoTracking()
                    .Where(x => x.DepartmentId == assignment.DepartmentId).Select(x => x.UserId).ToListAsync(cancellationToken);
                actorIds = operationalUsers.Where(x => x.Roles.Contains(AppRoles.Approver) && configured.Contains(x.User.Id))
                    .Select(x => x.User.Id).ToList();
            }
            else
            {
                var configured = await db.HumanResourcesScopes.AsNoTracking()
                    .Where(x => x.CompanyId == null || x.CompanyId == assignment.CompanyId).Select(x => x.UserId).ToListAsync(cancellationToken);
                actorIds = operationalUsers.Where(x => x.Roles.Contains(AppRoles.HumanResources) && configured.Contains(x.User.Id))
                    .Select(x => x.User.Id).ToList();
            }
            actorIds = actorIds.Where(x => x != requesterUserId).Distinct(StringComparer.Ordinal).ToList();
            if (actorIds.Count == 0)
            {
                var scope = level.Type == VacationApprovalLevelTypes.Approver ? "departamento" : "empresa";
                throw new InvalidOperationException($"El nivel '{level.Name}' no tiene usuarios habilitados para el {scope} del funcionario. Solicite al administrador que configure el circuito.");
            }
            steps.Add(new ApprovalStepSnapshot(
                level.Sequence,
                level.Name,
                level.Type,
                steps.Count == 0 ? VacationApprovalStepStatuses.Pending : VacationApprovalStepStatuses.Waiting,
                assignment.DepartmentId,
                assignment.CompanyId,
                actorIds));
        }
        return steps;
    }

    internal async Task<RequestSelfLink> GetSelfLinkAsync(CancellationToken cancellationToken = default)
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (!user.IsInRole(AppRoles.Employee))
            throw new UnauthorizedAccessException("No tiene acceso al autoservicio del funcionario.");
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employmentId = await globalDb.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.TenantId == tenant.Id && x.IsActive && x.ActiveEmploymentId != null)
            .Select(x => x.ActiveEmploymentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("La cuenta no está vinculada a un funcionario activo.");
        return new RequestSelfLink(userId, tenant.Id, tenant.SchemaName, employmentId);
    }

    internal async Task<RequestWorkflowActor> GetWorkflowActorAsync(
        bool requireOperationalRole,
        CancellationToken cancellationToken = default)
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var isAdministrator = user.IsInRole(AppRoles.TenantAdministrator);
        var isApprover = user.IsInRole(AppRoles.Approver);
        var isHumanResources = user.IsInRole(AppRoles.HumanResources);
        if ((requireOperationalRole && !(isApprover || isHumanResources)) ||
            (!requireOperationalRole && !(isAdministrator || isApprover || isHumanResources)))
            throw new UnauthorizedAccessException("No tiene acceso operativo al circuito de solicitudes.");
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await globalDb.Users.AsNoTracking().AnyAsync(
                x => x.Id == userId && x.TenantId == tenant.Id && x.IsActive,
                cancellationToken))
            throw new UnauthorizedAccessException("La cuenta no está activa en este tenant.");
        return new RequestWorkflowActor(userId, tenant.Id, tenant.SchemaName, isAdministrator, isApprover, isHumanResources);
    }

    internal async Task<Dictionary<string, string>> GetUserNamesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId && userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
    }

    private async Task<List<(VacationWorkflowUser User, HashSet<string> Roles)>> GetOperationalUsersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await (from user in db.Users.AsNoTracking()
                          join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                          join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                          where user.TenantId == tenantId && user.IsActive &&
                              (role.Name == AppRoles.Approver || role.Name == AppRoles.HumanResources)
                          select new { user.Id, user.DisplayName, user.Email, Role = role.Name! })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(x => new { x.Id, x.DisplayName, x.Email })
            .Select(x => (new VacationWorkflowUser(x.Key.Id, x.Key.DisplayName, x.Key.Email ?? string.Empty), x.Select(y => y.Role).ToHashSet(StringComparer.Ordinal)))
            .ToList();
    }

    private async Task<List<string>> ValidateOperationalUsersAsync(
        Guid tenantId,
        IReadOnlyCollection<string> userIds,
        string role,
        CancellationToken cancellationToken)
    {
        var distinct = userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        var valid = (await GetOperationalUsersAsync(tenantId, cancellationToken))
            .Where(x => x.Roles.Contains(role) && distinct.Contains(x.User.Id)).Select(x => x.User.Id).ToList();
        if (valid.Count != distinct.Count)
            throw new InvalidOperationException($"Uno de los usuarios no está activo o no tiene el rol {role}.");
        return valid;
    }

    private static List<VacationApprovalLevelInput> ValidateLevels(IReadOnlyCollection<VacationApprovalLevelInput> values)
    {
        var levels = values.Select(x => x with { Name = x.Name.Trim(), Type = x.Type.Trim().ToUpperInvariant() }).OrderBy(x => x.Sequence).ToList();
        if (levels.Count < 2 || levels.Any(x => string.IsNullOrWhiteSpace(x.Name)))
            throw new InvalidOperationException("El circuito necesita al menos los niveles Jefe y Recursos Humanos con nombre.");
        if (levels.Select(x => x.Sequence).Distinct().Count() != levels.Count || levels.Select(x => x.Sequence).Where((value, index) => value != index + 1).Any())
            throw new InvalidOperationException("Los niveles deben tener un orden consecutivo desde 1.");
        if (levels.Any(x => x.Type is not VacationApprovalLevelTypes.Approver and not VacationApprovalLevelTypes.HumanResources))
            throw new InvalidOperationException("El tipo de nivel no está admitido.");
        if (levels[0].Type != VacationApprovalLevelTypes.Approver || levels[^1].Type != VacationApprovalLevelTypes.HumanResources)
            throw new InvalidOperationException("El circuito debe comenzar con Jefe/Autorizador y terminar con Recursos Humanos.");
        var firstHr = levels.FindIndex(x => x.Type == VacationApprovalLevelTypes.HumanResources);
        if (levels.Skip(firstHr).Any(x => x.Type != VacationApprovalLevelTypes.HumanResources))
            throw new InvalidOperationException("Los niveles de Recursos Humanos deben ser posteriores a los niveles de Jefe/Autorizador.");
        return levels;
    }

    private async Task<(string UserId, Tenant Tenant)> GetAdministratorAsync(CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (!(await authorizationService.AuthorizeAsync(state.User, AppPolicies.TenantAdministration)).Succeeded)
            throw new UnauthorizedAccessException("Solo el administrador tenant puede configurar el circuito.");
        var userId = state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        return (userId, await currentTenant.GetRequiredAsync(cancellationToken));
    }
}
