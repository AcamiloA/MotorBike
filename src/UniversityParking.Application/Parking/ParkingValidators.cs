using FluentValidation;

namespace UniversityParking.Application.Parking;

public sealed class GetParkingAccessUserQueryValidator : AbstractValidator<GetParkingAccessUserQuery>
{
    public GetParkingAccessUserQueryValidator()
    {
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.CardCode) != string.IsNullOrWhiteSpace(x.IdentificationNumber))
            .WithMessage("Envía exactamente uno: código de carné o identificación.");
        RuleFor(x => x.CardCode).MaximumLength(150).WithMessage("El código de carné admite hasta 150 caracteres.");
        RuleFor(x => x.IdentificationNumber).MaximumLength(50).WithMessage("La identificación admite hasta 50 caracteres.");
    }
}
public sealed class CheckInVehicleCommandValidator : AbstractValidator<CheckInVehicleCommand>
{
    public CheckInVehicleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El vehículo es obligatorio.");
        RuleFor(x => x.ParkingLotId).NotEmpty().WithMessage("El parqueadero es obligatorio.");
    }
}
public sealed class CheckOutVehicleCommandValidator : AbstractValidator<CheckOutVehicleCommand>
{ public CheckOutVehicleCommandValidator() => RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El vehículo es obligatorio."); }
public abstract class ParkingPageValidator<T> : AbstractValidator<T> where T : IParkingPage
{
    protected ParkingPageValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
public sealed class GetVehiclesInsideQueryValidator : ParkingPageValidator<GetVehiclesInsideQuery>
{
    public GetVehiclesInsideQueryValidator()
    {
        RuleFor(x => x.ParkingLotId).NotEqual(Guid.Empty).When(x => x.ParkingLotId.HasValue).WithMessage("El parqueadero no es válido.");
        RuleFor(x => x.VehicleType).IsInEnum().When(x => x.VehicleType.HasValue).WithMessage("El tipo de vehículo no es válido.");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("La búsqueda admite hasta 200 caracteres.");
    }
}
public sealed class GetParkingMovementsQueryValidator : ParkingPageValidator<GetParkingMovementsQuery>
{
    public GetParkingMovementsQueryValidator()
    {
        RuleFor(x => x.ParkingLotId).NotEqual(Guid.Empty).When(x => x.ParkingLotId.HasValue).WithMessage("El parqueadero no es válido.");
        RuleFor(x => x.VehicleType).IsInEnum().When(x => x.VehicleType.HasValue).WithMessage("El tipo de vehículo no es válido.");
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue).WithMessage("El estado no es válido.");
        RuleFor(x => x.IdentificationNumber).MaximumLength(50).WithMessage("La identificación admite hasta 50 caracteres.");
        RuleFor(x => x.Plate).MaximumLength(100).WithMessage("La placa admite hasta 100 caracteres.");
        RuleFor(x => x.FrameNumber).MaximumLength(150).WithMessage("El número de marco admite hasta 150 caracteres.");
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom).When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("La fecha final debe ser mayor o igual a la inicial.");
    }
}
public sealed class GetMyParkingHistoryQueryValidator : ParkingPageValidator<GetMyParkingHistoryQuery>
{
    public GetMyParkingHistoryQueryValidator()
    {
        RuleFor(x => x.VehicleId).NotEqual(Guid.Empty).When(x => x.VehicleId.HasValue).WithMessage("El vehículo no es válido.");
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom).When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("La fecha final debe ser mayor o igual a la inicial.");
    }
}
