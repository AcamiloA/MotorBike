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
        RuleFor(x => x.Email).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Password).Cascade(CascadeMode.Stop).ExistingPasswordPolicy();
    }
}
