using FluentValidation;
using UniversityParking.Application.Common.Validation;

namespace UniversityParking.Application.Auth.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("La contraseña actual es obligatoria.");
        RuleFor(x => x.NewPassword).Cascade(CascadeMode.Stop).ExistingPasswordPolicy("La nueva contraseña es obligatoria.");
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser diferente de la actual.");
    }
}
