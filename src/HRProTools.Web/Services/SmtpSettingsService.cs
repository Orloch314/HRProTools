using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using HRProTools.Web.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace HRProTools.Web.Services;

public sealed class SmtpSettingsModel
{
    [Required, MaxLength(255)] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; } = 587;
    [Required] public string SecurityMode { get; set; } = "StartTls";
    [MaxLength(255)] public string Username { get; set; } = string.Empty;
    [DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string FromAddress { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string FromName { get; set; } = "HR ProTools";
    public bool HasStoredPassword { get; set; }
}

public sealed class SmtpSettingsService(
    ApplicationDbContext db,
    IDataProtectionProvider protectionProvider)
{
    private readonly IDataProtector _protector =
        protectionProvider.CreateProtector("HRProTools.SmtpPassword.v1");

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) =>
        await db.SmtpConfigurations.AsNoTracking()
            .AnyAsync(x => x.Id == 1 && x.Host != "" && x.FromAddress != "", cancellationToken);

    public async Task<SmtpSettingsModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        var value = await db.SmtpConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        return value is null ? new() : new()
        {
            Host = value.Host,
            Port = value.Port,
            SecurityMode = value.SecurityMode,
            Username = value.Username,
            FromAddress = value.FromAddress,
            FromName = value.FromName,
            HasStoredPassword = value.ProtectedPassword.Length > 0
        };
    }

    public async Task SaveAsync(SmtpSettingsModel model, string actor, CancellationToken cancellationToken = default)
    {
        var value = await db.SmtpConfigurations.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken)
            ?? new SmtpConfiguration();
        value.Host = model.Host.Trim();
        value.Port = model.Port;
        value.SecurityMode = model.SecurityMode;
        value.Username = model.Username.Trim();
        value.FromAddress = model.FromAddress.Trim();
        value.FromName = model.FromName.Trim();
        value.UpdatedAtUtc = DateTimeOffset.UtcNow;
        value.UpdatedBy = actor;
        if (!string.IsNullOrEmpty(model.Password))
            value.ProtectedPassword = _protector.Protect(model.Password);
        if (db.Entry(value).State == EntityState.Detached) db.Add(value);
        await db.SaveChangesAsync(cancellationToken);
        model.Password = string.Empty;
        model.HasStoredPassword = value.ProtectedPassword.Length > 0;
    }

    public async Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken = default)
        => _ = await SendWithReceiptAsync(recipient, subject, html, cancellationToken);

    public async Task<string> SendWithReceiptAsync(string recipient, string subject, string html, CancellationToken cancellationToken = default)
    {
        var settings = await db.SmtpConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken)
            ?? throw new InvalidOperationException("La configuracion SMTP no fue guardada.");
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new InvalidOperationException("La configuracion SMTP esta incompleta.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.ReplyTo.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            TextBody = ToPlainText(html),
            HtmlBody = html
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, ParseMode(settings.SecurityMode), cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var password = settings.ProtectedPassword.Length == 0 ? string.Empty : _protector.Unprotect(settings.ProtectedPassword);
            await client.AuthenticateAsync(settings.Username, password, cancellationToken);
        }
        var receipt = await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        return receipt;
    }

    private static SecureSocketOptions ParseMode(string value) => value switch
    {
        "None" => SecureSocketOptions.None,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls
    };

    private static string ToPlainText(string html)
    {
        var withLineBreaks = Regex.Replace(html, "<(br|/p|/div|/h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withLineBreaks, "<[^>]+>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }
}
