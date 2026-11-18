using Microsoft.AspNetCore.Identity;

namespace HRProTools.Web.Security;

public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "No fue posible completar la operación.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "La cuenta fue modificada por otro proceso. Intente nuevamente.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "La contraseña actual no es correcta.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "El enlace no es válido, venció o ya fue utilizado.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "El correo electrónico no es válido.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Ya existe una cuenta con ese correo electrónico.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "Ya existe una cuenta con ese correo electrónico.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe incluir al menos un símbolo.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "La contraseña debe incluir al menos un número.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "La contraseña debe incluir al menos una letra minúscula.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "La contraseña debe incluir al menos una letra mayúscula.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"La contraseña debe contener al menos {uniqueChars} caracteres diferentes.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
