using FluentValidation;

namespace UniversityParking.Application.Auth.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("La contraseña actual es obligatoria.");
        RuleFor(x => x.NewPassword).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .Must(value => value.Any(char.IsUpper)).WithMessage("La contraseña debe incluir una mayúscula.")
            .Must(value => value.Any(char.IsLower)).WithMessage("La contraseña debe incluir una minúscula.")
            .Must(value => value.Any(char.IsDigit)).WithMessage("La contraseña debe incluir un número.");
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser diferente de la actual.");
    }
}
