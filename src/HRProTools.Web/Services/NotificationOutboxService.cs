using System.Net;
using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record RequestNotificationContent(
    string RequestType,
    string EmployeeName,
    string Interval,
    string Quantity,
    string State,
    string PendingLevel,
    string DecisionReason,
    string RelativePath);

public sealed record NotificationOutboxSummary(int Pending, int Failed, int Sent, bool SmtpConfigured, string LastError);

public interface INotificationEmailTransport
{
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);
    Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken = default);
}

public sealed class SmtpNotificationEmailTransport(SmtpSettingsService smtp) : INotificationEmailTransport
{
    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) => smtp.IsConfiguredAsync(cancellationToken);
    public Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken = default) =>
        smtp.SendAsync(recipient, subject, html, cancellationToken);
}

public sealed class NotificationOutboxService(
    CurrentTenantService currentTenant,
    TenantDbContextFactory tenantFactory,
    SmtpSettingsService smtp,
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorizationService)
{
    public static void EnqueueRequestEvent(
        TenantDbContext db,
        string eventKey,
        string eventType,
        Guid requestId,
        IReadOnlyCollection<string> recipientUserIds,
        RequestNotificationContent content,
        string? publicBaseUrl)
    {
        var recipients = recipientUserIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        if (recipients.Count == 0) return;
        var requestLabel = content.RequestType == "VACATION" ? "vacaciones" : "permiso";
        var subject = $"HR ProTools · Solicitud de {requestLabel}: {content.State}";
        var link = BuildLink(publicBaseUrl, content.RelativePath);
        var html = $"<p>Se registró una novedad en una solicitud de {requestLabel}.</p>" +
            $"<p><strong>Funcionario:</strong> {Encode(content.EmployeeName)}<br>" +
            $"<strong>Fechas:</strong> {Encode(content.Interval)}<br>" +
            $"<strong>Cantidad:</strong> {Encode(content.Quantity)}<br>" +
            $"<strong>Estado:</strong> {Encode(content.State)}</p>" +
            (string.IsNullOrWhiteSpace(content.PendingLevel) ? string.Empty :
                $"<p><strong>Nivel pendiente:</strong> {Encode(content.PendingLevel)}</p>") +
            (string.IsNullOrWhiteSpace(content.DecisionReason) ? string.Empty :
                $"<p><strong>Motivo:</strong> {Encode(content.DecisionReason)}</p>") +
            (link is null ? string.Empty : $"<p><a href=\"{Encode(link)}\">Abrir HR ProTools</a></p>");

        foreach (var userId in recipients)
        {
            db.NotificationOutbox.Add(new NotificationOutboxItem
            {
                EventKey = $"{eventKey}:{userId}",
                EventType = eventType,
                RequestType = content.RequestType,
                RequestId = requestId,
                RecipientUserId = userId,
                Subject = subject,
                HtmlBody = html
            });
        }
    }

    public async Task<NotificationOutboxSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetAdministratorTenantAsync(cancellationToken);
        await using var db = tenantFactory.Create(tenant.SchemaName);
        var pending = await db.NotificationOutbox.CountAsync(x => x.Status == NotificationOutboxStatuses.Pending, cancellationToken);
        var failed = await db.NotificationOutbox.CountAsync(x => x.Status == NotificationOutboxStatuses.Failed, cancellationToken);
        var sent = await db.NotificationOutbox.CountAsync(x => x.Status == NotificationOutboxStatuses.Sent, cancellationToken);
        var lastError = await db.NotificationOutbox.AsNoTracking()
            .Where(x => x.LastError != string.Empty)
            .OrderByDescending(x => x.LastAttemptAtUtc)
            .Select(x => x.LastError)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        return new NotificationOutboxSummary(pending, failed, sent, await smtp.IsConfiguredAsync(cancellationToken), lastError);
    }

    public async Task RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetAdministratorTenantAsync(cancellationToken);
        await using var db = tenantFactory.Create(tenant.SchemaName);
        var failed = await db.NotificationOutbox.Where(x => x.Status == NotificationOutboxStatuses.Failed).ToListAsync(cancellationToken);
        foreach (var item in failed)
        {
            item.Status = NotificationOutboxStatuses.Pending;
            item.AttemptCount = 0;
            item.NextAttemptAtUtc = null;
            item.LastError = string.Empty;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Tenant> GetAdministratorTenantAsync(CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (!(await authorizationService.AuthorizeAsync(state.User, AppPolicies.TenantAdministration)).Succeeded ||
            state.User.FindFirstValue(ClaimTypes.NameIdentifier) is null)
            throw new UnauthorizedAccessException("Solo el administrador tenant puede consultar las notificaciones.");
        return await currentTenant.GetRequiredAsync(cancellationToken);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string? BuildLink(string? publicBaseUrl, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl)) return null;
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            return null;
        return new Uri(baseUri, relativePath.TrimStart('/')).ToString();
    }
}

public sealed class NotificationOutboxHostedService(
    IServiceScopeFactory scopeFactory,
    TenantDbContextFactory tenantFactory,
    ILogger<NotificationOutboxHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessOnceAsync(stoppingToken);
    }

    public async Task ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<INotificationEmailTransport>();
            if (!await email.IsConfiguredAsync(cancellationToken)) return;
            var tenants = await globalDb.Tenants.AsNoTracking()
                .Where(x => x.Status == TenantStatuses.Active)
                .Select(x => new { x.Id, x.SchemaName })
                .ToListAsync(cancellationToken);
            foreach (var tenant in tenants)
                await ProcessTenantAsync(globalDb, email, tenant.Id, tenant.SchemaName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo procesar la salida de notificaciones.");
        }
    }

    public async Task ProcessTenantOnceAsync(Guid tenantId, string schemaName, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var globalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<INotificationEmailTransport>();
        if (!await email.IsConfiguredAsync(cancellationToken)) return;
        await ProcessTenantAsync(globalDb, email, tenantId, schemaName, cancellationToken);
    }

    private async Task ProcessTenantAsync(
        ApplicationDbContext globalDb,
        INotificationEmailTransport emailTransport,
        Guid tenantId,
        string schemaName,
        CancellationToken cancellationToken)
    {
        await using var readDb = tenantFactory.Create(schemaName);
        var now = DateTimeOffset.UtcNow;
        var ids = await readDb.NotificationOutbox.AsNoTracking()
            .Where(x => x.Status == NotificationOutboxStatuses.Pending &&
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await using var db = tenantFactory.Create(schemaName);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var item = await db.NotificationOutbox
                .FromSqlInterpolated($"SELECT * FROM notification_outbox WHERE \"Id\" = {id} FOR UPDATE SKIP LOCKED")
                .SingleOrDefaultAsync(cancellationToken);
            if (item is null || item.Status != NotificationOutboxStatuses.Pending)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }
            item.AttemptCount++;
            item.LastAttemptAtUtc = DateTimeOffset.UtcNow;
            try
            {
                var email = await globalDb.Users.AsNoTracking()
                    .Where(x => x.Id == item.RecipientUserId && x.TenantId == tenantId && x.IsActive)
                    .Select(x => x.Email)
                    .SingleOrDefaultAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(email))
                    throw new InvalidOperationException("El destinatario no tiene un correo activo en este tenant.");
                await emailTransport.SendAsync(email, item.Subject, item.HtmlBody, cancellationToken);
                item.Status = NotificationOutboxStatuses.Sent;
                item.SentAtUtc = DateTimeOffset.UtcNow;
                item.NextAttemptAtUtc = null;
                item.LastError = string.Empty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                if (item.AttemptCount >= 10)
                    item.Status = NotificationOutboxStatuses.Failed;
                else
                    item.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, item.AttemptCount)));
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
