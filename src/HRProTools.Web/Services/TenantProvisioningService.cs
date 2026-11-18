using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class CreateTenantModel
{
    [Required, MaxLength(32)] public string GroupCode { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string GroupName { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string CompanyName { get; set; } = string.Empty;
    [Required, MaxLength(32)] public string TaxId { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string AdministratorName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string AdministratorEmail { get; set; } = string.Empty;
}

public sealed record ProvisionTenantResult(Tenant Tenant, bool EmailSent, string InvitationLink);

public sealed class TenantProvisioningService(
    ApplicationDbContext globalDb,
    TenantDbContextFactory tenantFactory,
    UserManager<ApplicationUser> users,
    SmtpSettingsService smtp,
    NavigationManager navigation,
    IConfiguration configuration)
{
    public async Task<ProvisionTenantResult> CreateAsync(
        CreateTenantModel input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = new Tenant
        {
            Code = input.GroupCode.Trim().ToUpperInvariant(),
            Name = input.GroupName.Trim()
        };
        tenant.SchemaName = TenantSchemaNames.For(tenant.Id);
        globalDb.Tenants.Add(tenant);
        await globalDb.SaveChangesAsync(cancellationToken);

        ApplicationUser? invited = null;
        try
        {
            // The identifier is generated from a server-side UUID and validated by TenantSchemaNames.
#pragma warning disable EF1003
            await globalDb.Database.ExecuteSqlRawAsync(
                "CREATE SCHEMA IF NOT EXISTS \"" + tenant.SchemaName + "\"", cancellationToken);
#pragma warning restore EF1003

            await using (var tenantDb = tenantFactory.Create(tenant.SchemaName))
            {
                await tenantDb.Database.MigrateAsync(cancellationToken);
                await using var transaction = await tenantDb.Database.BeginTransactionAsync(cancellationToken);
                var companyCode = await TenantIdentifierAllocator.NextCompanyCodeAsync(tenantDb, cancellationToken);
                tenantDb.Companies.Add(new Company
                {
                    Code = companyCode,
                    LegalName = input.CompanyName.Trim(),
                    TradeName = input.CompanyName.Trim(),
                    TaxId = input.TaxId.Trim()
                });
                await tenantDb.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            var email = input.AdministratorEmail.Trim().ToLowerInvariant();
            if (await users.FindByEmailAsync(email) is not null)
                throw new InvalidOperationException("Ya existe un usuario con ese correo electronico.");
            invited = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = input.AdministratorName.Trim(),
                TenantId = tenant.Id,
                EmailConfirmed = false,
                IsActive = false,
                InvitationSentAtUtc = DateTimeOffset.UtcNow
            };
            Ensure(await users.CreateAsync(invited));
            Ensure(await users.AddToRoleAsync(invited, AppRoles.TenantAdministrator));

            var token = await users.GeneratePasswordResetTokenAsync(invited);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var invitationLink = navigation.GetUriWithQueryParameters(
                GetInvitationEndpoint(),
                new Dictionary<string, object?> { ["code"] = code, ["email"] = email });

            tenant.Status = TenantStatuses.Active;
            tenant.ProvisioningError = null;
            tenant.UpdatedAtUtc = DateTimeOffset.UtcNow;
            globalDb.OperationLogs.Add(new OperationLog
            {
                ActorUserId = actorUserId,
                TenantId = tenant.Id,
                Action = "TenantCreated",
                Subject = tenant.Name,
                Detail = $"Empresa inicial {input.CompanyName.Trim()} creada."
            });
            await globalDb.SaveChangesAsync(cancellationToken);

            var sent = false;
            if (await smtp.IsConfiguredAsync(cancellationToken))
            {
                var safeTenantName = WebUtility.HtmlEncode(tenant.Name);
                var safeInvitationLink = WebUtility.HtmlEncode(invitationLink);
                await smtp.SendAsync(email, $"Invitación para administrar {tenant.Name} en HR ProTools",
                    $"""
                    <!doctype html>
                    <html lang="es"><body style="font-family:Arial,sans-serif;color:#10243a;line-height:1.5">
                    <h1 style="color:#08243c">Bienvenido a HR ProTools</h1>
                    <p>Fue invitado a administrar <strong>{safeTenantName}</strong>.</p>
                    <p><a href="{safeInvitationLink}" style="display:inline-block;background:#ffc400;color:#10243a;padding:12px 18px;border-radius:8px;text-decoration:none;font-weight:bold">Completar invitación</a></p>
                    <p>El enlace es personal, puede utilizarse una sola vez y vence en 24 horas.</p>
                    <p>Si no esperaba esta invitación, puede ignorar este mensaje.</p>
                    </body></html>
                    """,
                    cancellationToken);
                sent = true;
            }
            return new ProvisionTenantResult(tenant, sent, invitationLink);
        }
        catch (Exception ex)
        {
            if (invited is not null) await users.DeleteAsync(invited);
            tenant.Status = TenantStatuses.Error;
            tenant.ProvisioningError = ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000];
            tenant.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await globalDb.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private string GetInvitationEndpoint()
    {
        var configuredBaseUrl = configuration["Application:PublicBaseUrl"];
        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var publicBase) &&
            (publicBase.Scheme == Uri.UriSchemeHttp || publicBase.Scheme == Uri.UriSchemeHttps))
        {
            var normalizedBase = new Uri(publicBase.AbsoluteUri.TrimEnd('/') + "/");
            return new Uri(normalizedBase, "Account/AcceptInvitation").AbsoluteUri;
        }

        return navigation.ToAbsoluteUri("Account/AcceptInvitation").AbsoluteUri;
    }
}
