using FluentValidation;

namespace UniversityParking.Application.Reporting;

public abstract class AccessRangeValidator<T> : AbstractValidator<T> where T : IAccessRange
{
    protected AccessRangeValidator()
    {
        RuleFor(x => x.DateFrom).NotEqual(default(DateOnly)).WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.DateTo).NotEqual(default(DateOnly)).GreaterThanOrEqualTo(x => x.DateFrom).WithMessage("La fecha final debe ser mayor o igual a la inicial.");
        RuleFor(x => x.ParkingLotId).NotEqual(Guid.Empty).When(x => x.ParkingLotId.HasValue).WithMessage("El parqueadero no es válido.");
    }
}
public sealed class GetDailyAccessReportQueryValidator : AccessRangeValidator<GetDailyAccessReportQuery>;
public sealed class GetAccessByVehicleTypeReportQueryValidator : AccessRangeValidator<GetAccessByVehicleTypeReportQuery>;
public sealed class GetAccessByMemberTypeReportQueryValidator : AccessRangeValidator<GetAccessByMemberTypeReportQuery>;
public sealed class GetGuardActivityReportQueryValidator : AbstractValidator<GetGuardActivityReportQuery>
{
    public GetGuardActivityReportQueryValidator()
    {
        RuleFor(x => x.GuardUserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.DateFrom).NotEqual(default(DateOnly)).WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.DateTo).NotEqual(default(DateOnly)).GreaterThanOrEqualTo(x => x.DateFrom).WithMessage("La fecha final debe ser mayor o igual a la inicial.");
    }
}
public sealed class GetGuardDashboardQueryValidator : AbstractValidator<GetGuardDashboardQuery>
{ public GetGuardDashboardQueryValidator() => RuleFor(x => x.ParkingLotId).NotEmpty().WithMessage("El parqueadero es obligatorio."); }
public sealed class GetVehicleHistoryReportQueryValidator : AbstractValidator<GetVehicleHistoryReportQuery>
{
    public GetVehicleHistoryReportQueryValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("El vehículo es obligatorio.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
public sealed class GetUserHistoryReportQueryValidator : AbstractValidator<GetUserHistoryReportQuery>
{
    public GetUserHistoryReportQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
public sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(x => x.ActorUserId).NotEqual(Guid.Empty).When(x => x.ActorUserId.HasValue).WithMessage("El actor no es válido.");
        RuleFor(x => x.EntityId).NotEqual(Guid.Empty).When(x => x.EntityId.HasValue).WithMessage("La entidad no es válida.");
        RuleFor(x => x.Action).MaximumLength(100).WithMessage("La acción admite hasta 100 caracteres.");
        RuleFor(x => x.EntityType).MaximumLength(100).WithMessage("El tipo de entidad admite hasta 100 caracteres.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom).When(x => x.DateFrom.HasValue && x.DateTo.HasValue).WithMessage("La fecha final debe ser mayor o igual a la inicial.");
    }
}
