using HRProTools.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace HRProTools.Web.Services;

public sealed class ApplicationEmailSender(SmtpSettingsService smtp) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        smtp.SendAsync(email, "Confirme su cuenta de HR ProTools", $"<p>Confirme su cuenta: <a href=\"{confirmationLink}\">abrir enlace</a>.</p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        smtp.SendAsync(email, "Configure su acceso a HR ProTools", $"<p>Configure su contraseña: <a href=\"{resetLink}\">aceptar invitacion</a>.</p><p>El enlace vence en 24 horas.</p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        smtp.SendAsync(email, "Codigo de recuperacion de HR ProTools", $"<p>Su codigo es <strong>{resetCode}</strong>.</p>");
}
