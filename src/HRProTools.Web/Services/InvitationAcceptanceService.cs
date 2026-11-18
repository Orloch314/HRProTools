using System.ComponentModel.DataAnnotations;
using System.Text;
using HRProTools.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class AcceptInvitationModel
{
    [Required(ErrorMessage = "Ingrese una contraseña.")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "La contraseña y su confirmación no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed record InvitationAcceptanceResult(bool Succeeded, string Message);

public sealed class InvitationAcceptanceService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    ILogger<InvitationAcceptanceService> logger)
{
    private const string InvalidInvitationMessage = "La invitación no es válida, venció o ya fue utilizada.";

    public async Task<InvitationAcceptanceResult> AcceptAsync(
        string? email,
        string? encodedCode,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(encodedCode))
            return new(false, InvalidInvitationMessage);

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.InvitationAcceptedAtUtc is not null || user.IsActive)
            return new(false, InvalidInvitationMessage);

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedCode));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return new(false, InvalidInvitationMessage);
        }

        var tokenIsValid = await users.VerifyUserTokenAsync(
            user,
            users.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose,
            token);
        if (!tokenIsValid)
            return new(false, InvalidInvitationMessage);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var passwordResult = await users.ResetPasswordAsync(user, token, password);
            if (!passwordResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, string.Join(" ", passwordResult.Errors.Select(error => error.Description)));
            }

            user.EmailConfirmed = true;
            user.IsActive = true;
            user.InvitationAcceptedAtUtc = DateTimeOffset.UtcNow;
            var activationResult = await users.UpdateAsync(user);
            if (!activationResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, "No fue posible activar la cuenta. Intente nuevamente.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new(true, "Su cuenta fue activada correctamente. Ya puede iniciar sesión.");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "No fue posible completar la invitación del usuario {UserId}.", user.Id);
            return new(false, "No fue posible activar la cuenta. Intente nuevamente o solicite una nueva invitación.");
        }
    }
}
