using FluentValidation;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Users;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.IdentificationNumber).NotEmpty().WithMessage("La identificación es obligatoria.").MaximumLength(50).WithMessage("La identificación admite hasta 50 caracteres.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
        RuleFor(x => x.UniversityId).NotEmpty().WithMessage("Selecciona una universidad.");
        RuleFor(x => x.Career).MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
        RuleFor(x => x.Career).NotEmpty().When(x => x.MemberType == MemberType.STUDENT)
            .WithMessage("La carrera es obligatoria para estudiantes.");
        RuleFor(x => x.MemberType).IsInEnum().WithMessage("El valor no es válido.");
        RuleFor(x => x.CardCode).NotEmpty().WithMessage("El código de carné es obligatorio.").MaximumLength(150).WithMessage("El código de carné admite hasta 150 caracteres.");
        RuleFor(x => x.InitialPassword).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("La contraseña es obligatoria.").MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .Must(x => x.Any(char.IsUpper)).WithMessage("La contraseña debe incluir una mayúscula.")
            .Must(x => x.Any(char.IsLower)).WithMessage("La contraseña debe incluir una minúscula.")
            .Must(x => x.Any(char.IsDigit)).WithMessage("La contraseña debe incluir un número.");
        RuleForEach(x => x.Roles).Must(UserValidation.IsRole).WithMessage("El rol no es válido.");
    }
}
public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
        RuleFor(x => x.Career).MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
    }
}
public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El identificador es obligatorio.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
        RuleFor(x => x.UniversityId).NotEmpty().WithMessage("Selecciona una universidad.");
        RuleFor(x => x.Career).MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
        RuleFor(x => x.Career).NotEmpty().When(x => x.MemberType == MemberType.STUDENT)
            .WithMessage("La carrera es obligatoria para estudiantes.");
        RuleFor(x => x.MemberType).IsInEnum().WithMessage("El valor no es válido.");
        RuleFor(x => x.CardCode).NotEmpty().WithMessage("El código de carné es obligatorio.").MaximumLength(150).WithMessage("El código de carné admite hasta 150 caracteres.");
    }
}
public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El identificador es obligatorio.");
        RuleFor(x => x.RoleCode).Must(UserValidation.IsRole).WithMessage("El rol no es válido.");
    }
}
public sealed class RemoveRoleCommandValidator : AbstractValidator<RemoveRoleCommand>
{
    public RemoveRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El identificador es obligatorio.");
        RuleFor(x => x.RoleCode).Must(UserValidation.IsRole).WithMessage("El rol no es válido.");
    }
}
public sealed class ActivateUserCommandValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserCommandValidator() => RuleFor(x => x.UserId).NotEmpty().WithMessage("El identificador es obligatorio.");
}
public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator() => RuleFor(x => x.UserId).NotEmpty().WithMessage("El identificador es obligatorio.");
}
public sealed class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdQueryValidator() => RuleFor(x => x.UserId).NotEmpty().WithMessage("El identificador es obligatorio.");
}
public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
        RuleFor(x => x.MemberType).IsInEnum().WithMessage("El valor no es válido.").When(x => x.MemberType.HasValue);
        RuleFor(x => x.Status).IsInEnum().WithMessage("El valor no es válido.").When(x => x.Status.HasValue);
        RuleFor(x => x.Role).Must(UserValidation.IsRole).WithMessage("El rol no es válido.").When(x => x.Role is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
internal static class UserValidation
{
    public static bool IsRole(string? code) => code is RoleCodes.User or RoleCodes.Guard or RoleCodes.Admin;
}

