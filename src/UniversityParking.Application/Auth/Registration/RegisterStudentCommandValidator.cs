using FluentValidation;
using UniversityParking.Application.Common.Validation;

namespace UniversityParking.Application.Auth.Registration;

public sealed class RegisterStudentCommandValidator : AbstractValidator<RegisterStudentCommand>
{
    public RegisterStudentCommandValidator()
    {
        RuleFor(x => x.IdentificationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UniversityId).NotEmpty().WithMessage("Selecciona una universidad.");
        RuleFor(x => x.Career).NotEmpty().WithMessage("La carrera es obligatoria para estudiantes.").MaximumLength(200);
        RuleFor(x => x.CardCode).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Password).Cascade(CascadeMode.Stop).ExistingPasswordPolicy();
    }
}
