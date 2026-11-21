using System.Net;
using System.Security.Claims;
using System.Text;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record EmployeeAccessInfo(string Email, string Status, IReadOnlyList<string> Roles, DateTimeOffset? InvitationSentAtUtc);
public sealed record EmployeeInvitationResult(bool EmailSent, string InvitationLink, string Message);
public sealed record EmployeeSelfServiceData(string FullName, string DocumentNumber, string EmployeeNumber, string Company, string Department, string Position, decimal AvailableVacationDays, decimal ReservedVacationDays);

public sealed class EmployeeAccessService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory tenantFactory,
    IServiceScopeFactory scopeFactory,
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorizationService,
    NavigationManager navigation,
    IConfiguration configuration)
{
    public async Task<EmployeeAccessInfo?> GetAsync(Guid employmentId, CancellationToken cancellationToken = default)
    {
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var tenantDb = tenantFactory.Create(tenant.SchemaName);
        var personId = await tenantDb.Employments.Where(x => x.Id == employmentId).Select(x => (Guid?)x.PersonId).SingleOrDefaultAsync(cancellationToken);
        if (personId is null) return null;
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.Users.SingleOrDefaultAsync(x => x.TenantId == tenant.Id && x.PersonId == personId, cancellationToken);
        if (user is null) return new EmployeeAccessInfo(string.Empty, "Sin usuario", [], null);
        var roles = await users.GetRolesAsync(user);
        return new EmployeeAccessInfo(user.Email ?? string.Empty, Status(user), roles.Order().ToList(), user.InvitationSentAtUtc);
    }

    public async Task<EmployeeInvitationResult> InviteAsync(Guid employmentId, CancellationToken cancellationToken = default)
    {
        await EnsureTenantAdministratorAsync();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var tenantDb = tenantFactory.Create(tenant.SchemaName);
        var employee = await tenantDb.Employments.AsNoTracking().Where(x => x.Id == employmentId && x.Status == EmploymentStatuses.Active)
            .Select(x => new { x.Id, x.PersonId, x.Person.Email, Name = x.Person.GivenNames + " " + x.Person.FamilyNames })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("El vínculo laboral no está activo.");
        var email = employee.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("El funcionario necesita un correo electrónico para recibir la invitación.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var smtp = scope.ServiceProvider.GetRequiredService<SmtpSettingsService>();
        var user = await users.FindByEmailAsync(email);
        if (user is not null && (user.TenantId != tenant.Id || (user.PersonId is not null && user.PersonId != employee.PersonId)))
            throw new InvalidOperationException("El correo ya pertenece a otra cuenta y no puede asociarse automáticamente.");
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, DisplayName = employee.Name, TenantId = tenant.Id, PersonId = employee.PersonId, ActiveEmploymentId = employee.Id, IsActive = false, EmailConfirmed = false };
            Ensure(await users.CreateAsync(user));
        }
        else
        {
            user.PersonId = employee.PersonId;
            user.ActiveEmploymentId = employee.Id;
            user.DisplayName = employee.Name;
            Ensure(await users.UpdateAsync(user));
            Ensure(await users.UpdateSecurityStampAsync(user));
        }
        if (!await users.IsInRoleAsync(user, AppRoles.Employee)) Ensure(await users.AddToRoleAsync(user, AppRoles.Employee));
        user.InvitationSentAtUtc = DateTimeOffset.UtcNow;
        Ensure(await users.UpdateAsync(user));
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = BuildInvitationLink(code, email);
        if (!await smtp.IsConfiguredAsync(cancellationToken))
            return new(false, link, "Cuenta preparada. SMTP no está configurado; utilice el enlace manual.");
        try
        {
            await smtp.SendAsync(email, $"Invitación para acceder a {tenant.Name} en HR ProTools",
                $"<h1>Bienvenido a HR ProTools</h1><p>Hola {WebUtility.HtmlEncode(employee.Name)}, configure su acceso:</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Completar invitación</a></p><p>El enlace vence en 24 horas y reemplaza cualquier invitación anterior.</p>", cancellationToken);
            return new(true, link, "Invitación enviada.");
        }
        catch (Exception ex)
        {
            return new(false, link, $"La cuenta quedó preparada, pero el correo no pudo enviarse: {ex.Message}");
        }
    }

    public async Task SetActiveAsync(Guid employmentId, bool active, CancellationToken cancellationToken = default)
    {
        await EnsureTenantAdministratorAsync();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var tenantDb = tenantFactory.Create(tenant.SchemaName);
        var personId = await tenantDb.Employments.Where(x => x.Id == employmentId).Select(x => x.PersonId).SingleAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.Users.SingleAsync(x => x.TenantId == tenant.Id && x.PersonId == personId, cancellationToken);
        if (active && (!user.EmailConfirmed || !await users.HasPasswordAsync(user)))
            throw new InvalidOperationException("La cuenta debe completar la invitación antes de reactivarse.");
        user.IsActive = active;
        user.ActiveEmploymentId = active ? employmentId : user.ActiveEmploymentId;
        Ensure(await users.UpdateAsync(user));
        Ensure(await users.UpdateSecurityStampAsync(user));
    }

    public async Task AssociateExistingAsync(Guid employmentId, string email, CancellationToken cancellationToken = default)
    {
        await EnsureTenantAdministratorAsync();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var tenantDb = tenantFactory.Create(tenant.SchemaName);
        var employee = await tenantDb.Employments.Where(x => x.Id == employmentId).Select(x => new { x.Id, x.PersonId }).SingleAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email.Trim().ToLowerInvariant()) ?? throw new InvalidOperationException("No existe una cuenta con ese correo.");
        if (user.TenantId != tenant.Id || (user.PersonId is not null && user.PersonId != employee.PersonId))
            throw new InvalidOperationException("La cuenta no pertenece a la misma persona y tenant.");
        user.PersonId = employee.PersonId;
        user.ActiveEmploymentId = employee.Id;
        Ensure(await users.UpdateAsync(user));
        if (!await users.IsInRoleAsync(user, AppRoles.Employee)) Ensure(await users.AddToRoleAsync(user, AppRoles.Employee));
    }

    public async Task SetRolesAsync(Guid employmentId, IReadOnlyCollection<string> requestedRoles, CancellationToken cancellationToken = default)
    {
        await EnsureTenantAdministratorAsync();
        var allowed = new HashSet<string>([AppRoles.Employee, AppRoles.Approver, AppRoles.HumanResources, AppRoles.TenantAdministrator], StringComparer.Ordinal);
        if (requestedRoles.Any(x => !allowed.Contains(x))) throw new InvalidOperationException("La selección contiene un rol no permitido.");
        var desired = requestedRoles.Append(AppRoles.Employee).ToHashSet(StringComparer.Ordinal);
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var tenantDb = tenantFactory.Create(tenant.SchemaName);
        var personId = await tenantDb.Employments.Where(x => x.Id == employmentId).Select(x => x.PersonId).SingleAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.Users.SingleAsync(x => x.TenantId == tenant.Id && x.PersonId == personId, cancellationToken);
        var current = await users.GetRolesAsync(user);
        var remove = current.Where(x => allowed.Contains(x) && !desired.Contains(x)).ToList();
        var add = desired.Where(x => !current.Contains(x)).ToList();
        if (remove.Count > 0) Ensure(await users.RemoveFromRolesAsync(user, remove));
        if (add.Count > 0) Ensure(await users.AddToRolesAsync(user, add));
        Ensure(await users.UpdateSecurityStampAsync(user));
    }

    public async Task<EmployeeSelfServiceData> GetSelfAsync(CancellationToken cancellationToken = default)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userId = state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var tenant = await currentTenant.GetRequiredAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = await globalDb.Users.AsNoTracking().Where(x => x.Id == userId && x.TenantId == tenant.Id && x.IsActive)
            .Select(x => new { x.PersonId, x.ActiveEmploymentId }).SingleAsync(cancellationToken);
        if (link.PersonId is null || link.ActiveEmploymentId is null) throw new UnauthorizedAccessException("La cuenta no está vinculada a un funcionario activo.");
        await using var db = tenantFactory.Create(tenant.SchemaName);
        var data = await db.Employments.AsNoTracking().Where(x => x.Id == link.ActiveEmploymentId && x.PersonId == link.PersonId)
            .Select(x => new
            {
                FullName = x.Person.GivenNames + " " + x.Person.FamilyNames,
                x.Person.DocumentNumber,
                x.EmployeeNumber,
                Company = x.Company.LegalName,
                Department = db.EmploymentAssignments.Where(a => a.EmploymentId == x.Id && a.ValidTo == null).Select(a => a.Department.Name).FirstOrDefault() ?? "-",
                Position = db.EmploymentAssignments.Where(a => a.EmploymentId == x.Id && a.ValidTo == null).Select(a => a.Position.Name).FirstOrDefault() ?? "-"
            })
            .SingleAsync(cancellationToken);
        var balances = await VacationLedgerQueries.GetEmploymentBalanceAsync(db, link.ActiveEmploymentId.Value, cancellationToken);
        return new(data.FullName, data.DocumentNumber, data.EmployeeNumber, data.Company, data.Department, data.Position, balances.Available, balances.Reserved);
    }

    private string BuildInvitationLink(string code, string email)
    {
        var baseUrl = configuration["Application:PublicBaseUrl"];
        var endpoint = Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed)
            ? new Uri(new Uri(parsed.AbsoluteUri.TrimEnd('/') + "/"), "Account/AcceptInvitation").AbsoluteUri
            : navigation.ToAbsoluteUri("Account/AcceptInvitation").AbsoluteUri;
        return QueryHelpers.AddQueryString(endpoint, new Dictionary<string, string?> { ["code"] = code, ["email"] = email });
    }

    private static string Status(ApplicationUser user) => user.IsActive ? "Activo" : user.EmailConfirmed ? "Inactivo" : "Invitación pendiente";
    private async Task EnsureTenantAdministratorAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (!(await authorizationService.AuthorizeAsync(state.User, AppPolicies.TenantAdministration)).Succeeded)
            throw new UnauthorizedAccessException("No tiene permiso para administrar cuentas de funcionarios.");
    }
    private static void Ensure(IdentityResult result) { if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description))); }
}
