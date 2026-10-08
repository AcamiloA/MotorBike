using FluentValidation;
using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.ParkingLots;

public sealed record CreateParkingLotCommand(string Name, string Campus, TimeOnly OpeningTime, TimeOnly ClosingTime) : ICommand<Guid>;
public sealed record UpdateParkingLotCommand(Guid ParkingLotId, string Name, string Campus, TimeOnly OpeningTime, TimeOnly ClosingTime) : ICommand;
public sealed record ActivateParkingLotCommand(Guid ParkingLotId) : ICommand;
public sealed record DeactivateParkingLotCommand(Guid ParkingLotId) : ICommand;
public sealed record GetActiveParkingLotsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<ParkingLotView>>;
public sealed record GetParkingLotsQuery(ParkingLotStatus? Status = null, int Page = 1, int PageSize = 20) : IQuery<PagedResult<ParkingLotView>>;
public sealed record ParkingZoneView(Guid Id, string Name, VehicleType VehicleType, ParkingZoneStatus Status);
public sealed record ParkingLotView(Guid Id, string Name, string Campus, TimeOnly OpeningTime, TimeOnly ClosingTime,
    ParkingLotStatus Status, IReadOnlyList<ParkingZoneView> Zones);
public sealed class CreateParkingLotCommandValidator : AbstractValidator<CreateParkingLotCommand>
{
    public CreateParkingLotCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(150).WithMessage("El nombre admite hasta 150 caracteres.");
        RuleFor(x => x.Campus).NotEmpty().WithMessage("La sede es obligatoria.").MaximumLength(150).WithMessage("La sede admite hasta 150 caracteres.");
        RuleFor(x => x.OpeningTime).LessThan(x => x.ClosingTime).WithMessage("La apertura debe ser anterior al cierre en el mismo día.");
    }
}
public sealed class UpdateParkingLotCommandValidator : AbstractValidator<UpdateParkingLotCommand>
{
    public UpdateParkingLotCommandValidator()
    {
        RuleFor(x => x.ParkingLotId).NotEmpty().WithMessage("El identificador es obligatorio.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(150).WithMessage("El nombre admite hasta 150 caracteres.");
        RuleFor(x => x.Campus).NotEmpty().WithMessage("La sede es obligatoria.").MaximumLength(150).WithMessage("La sede admite hasta 150 caracteres.");
        RuleFor(x => x.OpeningTime).LessThan(x => x.ClosingTime).WithMessage("La apertura debe ser anterior al cierre en el mismo día.");
    }
}
public sealed class ActivateParkingLotCommandValidator : AbstractValidator<ActivateParkingLotCommand>
{ public ActivateParkingLotCommandValidator() => RuleFor(x => x.ParkingLotId).NotEmpty().WithMessage("El identificador es obligatorio."); }
public sealed class DeactivateParkingLotCommandValidator : AbstractValidator<DeactivateParkingLotCommand>
{ public DeactivateParkingLotCommandValidator() => RuleFor(x => x.ParkingLotId).NotEmpty().WithMessage("El identificador es obligatorio."); }
public sealed class GetParkingLotsQueryValidator : AbstractValidator<GetParkingLotsQuery>
{
    public GetParkingLotsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue).WithMessage("El estado no es válido.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
public sealed class GetActiveParkingLotsQueryValidator : AbstractValidator<GetActiveParkingLotsQuery>
{
    public GetActiveParkingLotsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
