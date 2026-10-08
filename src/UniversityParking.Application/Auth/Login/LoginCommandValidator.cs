using FluentValidation;

namespace UniversityParking.Application.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.IdentificationNumber).NotEmpty().MaximumLength(50).WithMessage("La identificación es obligatoria y admite hasta 50 caracteres.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
