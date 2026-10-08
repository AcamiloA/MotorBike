using FluentValidation;

namespace UniversityParking.Application.Incidents;

public sealed class CreateIncidentCommandValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentCommandValidator()
    {
        RuleFor(x => x.ParkingLotId).NotEmpty().WithMessage("El parqueadero es obligatorio.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("El tipo de incidente no es válido.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("La descripción es obligatoria.");
        RuleFor(x => x.UserId).NotEqual(Guid.Empty).When(x => x.UserId.HasValue).WithMessage("El usuario no es válido.");
        RuleFor(x => x.VehicleId).NotEqual(Guid.Empty).When(x => x.VehicleId.HasValue).WithMessage("El vehículo no es válido.");
        RuleFor(x => x.ParkingMovementId).NotEqual(Guid.Empty).When(x => x.ParkingMovementId.HasValue).WithMessage("El movimiento no es válido.");
    }
}
public sealed class ResolveIncidentCommandValidator : AbstractValidator<ResolveIncidentCommand>
{
    public ResolveIncidentCommandValidator() { RuleFor(x => x.IncidentId).NotEmpty().WithMessage("El incidente es obligatorio."); RuleFor(x => x.Resolution).NotEmpty().WithMessage("La resolución es obligatoria."); }
}
public sealed class CancelIncidentCommandValidator : AbstractValidator<CancelIncidentCommand>
{ public CancelIncidentCommandValidator() => RuleFor(x => x.IncidentId).NotEmpty().WithMessage("El incidente es obligatorio."); }
public sealed class GetIncidentByIdQueryValidator : AbstractValidator<GetIncidentByIdQuery>
{ public GetIncidentByIdQueryValidator() => RuleFor(x => x.IncidentId).NotEmpty().WithMessage("El incidente es obligatorio."); }
public sealed class GetIncidentAttachmentContentQueryValidator : AbstractValidator<GetIncidentAttachmentContentQuery>
{
    public GetIncidentAttachmentContentQueryValidator() { RuleFor(x => x.IncidentId).NotEmpty().WithMessage("El incidente es obligatorio."); RuleFor(x => x.AttachmentId).NotEmpty().WithMessage("El adjunto es obligatorio."); }
}
public sealed class GetIncidentsQueryValidator : AbstractValidator<GetIncidentsQuery>
{
    public GetIncidentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue).WithMessage("El estado no es válido.");
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type.HasValue).WithMessage("El tipo de incidente no es válido.");
        RuleFor(x => x.ParkingLotId).NotEqual(Guid.Empty).When(x => x.ParkingLotId.HasValue).WithMessage("El parqueadero no es válido.");
        RuleFor(x => x.UserId).NotEqual(Guid.Empty).When(x => x.UserId.HasValue).WithMessage("El usuario no es válido.");
        RuleFor(x => x.VehicleId).NotEqual(Guid.Empty).When(x => x.VehicleId.HasValue).WithMessage("El vehículo no es válido.");
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom).When(x => x.DateFrom.HasValue && x.DateTo.HasValue).WithMessage("La fecha final debe ser mayor o igual a la inicial.");
    }
}
