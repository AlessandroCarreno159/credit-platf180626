using Microsoft.AspNetCore.Identity;

#pragma warning disable CS8765 // Los overrides heredan la nulabilidad del IdentityErrorDescriber base.
namespace credit_platf.Services;

// Traduce al español los errores que devuelve UserManager (contraseñas, duplicados, etc.).
public class DescripcionErroresIdentity : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName)
        => new() { Code = nameof(DuplicateUserName), Description = $"El usuario '{userName}' ya está registrado." };

    public override IdentityError DuplicateEmail(string email)
        => new() { Code = nameof(DuplicateEmail), Description = $"El correo '{email}' ya está registrado." };

    public override IdentityError InvalidEmail(string email)
        => new() { Code = nameof(InvalidEmail), Description = $"El correo '{email}' no es válido." };

    public override IdentityError DuplicateRoleName(string role)
        => new() { Code = nameof(DuplicateRoleName), Description = $"El rol '{role}' ya existe." };

    public override IdentityError PasswordTooShort(int length)
        => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe tener al menos un carácter no alfanumérico." };

    public override IdentityError PasswordRequiresDigit()
        => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe tener al menos un dígito ('0'-'9')." };

    public override IdentityError PasswordRequiresLower()
        => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe tener al menos una minúscula ('a'-'z')." };

    public override IdentityError PasswordRequiresUpper()
        => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe tener al menos una mayúscula ('A'-'Z')." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"La contraseña debe tener al menos {uniqueChars} caracteres distintos." };

    public override IdentityError InvalidUserName(string userName)
        => new() { Code = nameof(InvalidUserName), Description = $"El nombre de usuario '{userName}' no es válido." };

    public override IdentityError InvalidToken()
        => new() { Code = nameof(InvalidToken), Description = "El token no es válido." };

    public override IdentityError UserAlreadyHasPassword()
        => new() { Code = nameof(UserAlreadyHasPassword), Description = "El usuario ya tiene una contraseña." };

    public override IdentityError UserAlreadyInRole(string role)
        => new() { Code = nameof(UserAlreadyInRole), Description = $"El usuario ya pertenece al rol '{role}'." };

    public override IdentityError UserNotInRole(string role)
        => new() { Code = nameof(UserNotInRole), Description = $"El usuario no pertenece al rol '{role}'." };
}
