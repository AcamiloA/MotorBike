using FluentValidation;

namespace UniversityParking.Application.Common.Validation;

public static class PasswordValidation
{
    public static IRuleBuilderOptions<T, string> ExistingPasswordPolicy<T>(this IRuleBuilder<T, string> rule,
        string requiredMessage = "La contraseña es obligatoria.") => rule
        .NotEmpty().WithMessage(requiredMessage)
        .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
        .Must(x => x.Any(char.IsUpper)).WithMessage("La contraseña debe incluir una mayúscula.")
        .Must(x => x.Any(char.IsLower)).WithMessage("La contraseña debe incluir una minúscula.")
        .Must(x => x.Any(char.IsDigit)).WithMessage("La contraseña debe incluir un número.");
}
