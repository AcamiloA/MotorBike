using FluentValidation;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Vehicles;

public sealed class RegisterVehicleCommandValidator : AbstractValidator<RegisterVehicleCommand>
{
    public RegisterVehicleCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("El tipo de vehículo no es válido.");
        RuleFor(x => x.Brand).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(100).WithMessage("La marca es obligatoria y admite hasta 100 caracteres.");
        RuleFor(x => x.Model).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(100).WithMessage("El modelo es obligatorio y admite hasta 100 caracteres.");
        RuleFor(x => x.Color).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(100).WithMessage("El color es obligatorio y admite hasta 100 caracteres.");
        RuleFor(x => x.Plate).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(100).When(x => x.Type is not (VehicleType.BICYCLE or VehicleType.SCOOTER)).WithMessage("La placa es obligatoria.");
        RuleFor(x => x.Plate).Empty().When(x => x.Type is VehicleType.BICYCLE or VehicleType.SCOOTER).WithMessage("Una bicicleta no utiliza placa.");
        RuleFor(x => x.FrameNumber).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(150).When(x => x.Type is VehicleType.BICYCLE or VehicleType.SCOOTER).WithMessage("El número de marco es obligatorio.");
        RuleFor(x => x.FrameNumber).Empty().When(x => x.Type is not (VehicleType.BICYCLE or VehicleType.SCOOTER)).WithMessage("El vehículo utiliza placa y no número de marco.");
        RuleFor(x => x.VerificationImage).NotNull().WithMessage("Selecciona la evidencia de verificación.");
    }
}
public sealed class DocumentUploadValidator : AbstractValidator<DocumentUpload>
{
    public DocumentUploadValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("El tipo de documento no es válido.");
        RuleFor(x => x.File).NotNull().WithMessage("El campo es obligatorio.");
        RuleFor(x => x.DocumentNumber).MaximumLength(100).WithMessage("El número de documento admite hasta 100 caracteres.");
        RuleFor(x => x.ExpiresOn).GreaterThanOrEqualTo(x => x.IssuedOn)
            .When(x => x.IssuedOn.HasValue && x.ExpiresOn.HasValue).WithMessage("El vencimiento no puede ser anterior a la emisión.");
    }
}
public sealed class RenewVehicleRegistrationCommandValidator : AbstractValidator<RenewVehicleRegistrationCommand>
{
    public RenewVehicleRegistrationCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El campo es obligatorio.");
        RuleFor(x => x.Documents).NotNull().WithMessage("El campo es obligatorio.");
        RuleForEach(x => x.Documents).SetValidator(new DocumentUploadValidator());
        RuleFor(x => x.Documents).Must(x => x is not null && x.All(d => d?.File is not null) && x.Sum(d => (decimal)d.File.SizeBytes) <= 50 * 1024 * 1024)
            .WithMessage("Los archivos no pueden superar 50 MB.");
    }
}
public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El campo es obligatorio.");
        RuleFor(x => x.Brand).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(100).WithMessage("El campo admite hasta 100 caracteres.");
        RuleFor(x => x.Model).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(100).WithMessage("El campo admite hasta 100 caracteres.");
        RuleFor(x => x.Color).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(100).WithMessage("El campo admite hasta 100 caracteres.");
    }
}
public sealed class CorrectVehicleIdentifierCommandValidator : AbstractValidator<CorrectVehicleIdentifierCommand>
{
    public CorrectVehicleIdentifierCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El campo es obligatorio.");
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(150).WithMessage("El campo admite hasta 150 caracteres.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(500).WithMessage("El campo admite hasta 500 caracteres.");
    }
}
public sealed class TransferVehicleCommandValidator : AbstractValidator<TransferVehicleCommand>
{
    public TransferVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El campo es obligatorio.");
        RuleFor(x => x.NewOwnerIdentificationNumber).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(50).WithMessage("El campo admite hasta 50 caracteres.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El campo es obligatorio.").MaximumLength(500).WithMessage("El campo admite hasta 500 caracteres.");
    }
}
public sealed class ActivateVehicleCommandValidator : AbstractValidator<ActivateVehicleCommand>
{ public ActivateVehicleCommandValidator() => RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El campo es obligatorio."); }
public sealed class DeactivateVehicleCommandValidator : AbstractValidator<DeactivateVehicleCommand>
{ public DeactivateVehicleCommandValidator() => RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El campo es obligatorio."); }
public sealed class GetVehicleByIdQueryValidator : AbstractValidator<GetVehicleByIdQuery>
{ public GetVehicleByIdQueryValidator() => RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El campo es obligatorio."); }
public sealed class GetVehiclesQueryValidator : AbstractValidator<GetVehiclesQuery>
{
    public GetVehiclesQueryValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("El valor no es válido.").When(x => x.Type.HasValue);
        RuleFor(x => x.Status).IsInEnum().WithMessage("El valor no es válido.").When(x => x.Status.HasValue);
        RuleFor(x => x.RegistrationState).IsInEnum().WithMessage("El valor no es válido.").When(x => x.RegistrationState.HasValue);
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("El campo admite hasta 200 caracteres.");
        RuleFor(x => x.OwnerIdentificationNumber).MaximumLength(50).WithMessage("El campo admite hasta 50 caracteres.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
