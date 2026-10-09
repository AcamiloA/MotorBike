using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Parking;

public sealed class GetParkingAccessUserQueryHandler(AdministrationOperationContext operation, IUserRepository users,
    IParkingMovementRepository movements, IParkingMovementReadRepository reads, IVehicleRepository vehicles,
    IAcademicPeriodRepository periods, IClock clock, IQrIdentityParser parser) : IRequestHandler<GetParkingAccessUserQuery, Result<ParkingAccessView>>
{
    public async Task<Result<ParkingAccessView>> Handle(GetParkingAccessUserQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<ParkingAccessView>.Failure(permission);
        if ((request.QrPayload is null) == (request.IdentificationNumber is null))
            return Result<ParkingAccessView>.Failure(CommonErrors.Validation(new Dictionary<string, IReadOnlyList<string>> { ["Identity"] = ["Envía exactamente uno: QrPayload o identificación."] }));
        var identity = request.QrPayload is not null ? parser.Parse(request.QrPayload) : Result<IdentificationNumber>.Success(new(request.IdentificationNumber!));
        if (identity.IsFailure) return Result<ParkingAccessView>.Failure(identity.Error!);
        var user = await users.GetByIdentificationNumberAsync(identity.Value, cancellationToken);
        if (user is null) return Result<ParkingAccessView>.Failure(UserErrors.NotFound);
        var summary = new ParkingAccessUserView(user.Id, user.FullName, user.MemberType, user.Status);
        var open = await movements.GetOpenByUserIdAsync(user.Id, cancellationToken);
        if (open is not null)
        {
            var current = await vehicles.GetViewAsync(open.VehicleId, cancellationToken);
            return Result<ParkingAccessView>.Success(new(summary, await reads.GetByIdAsync(open.Id, clock.UtcNow, cancellationToken), [],
                current is null ? null : Vehicle(current)));
        }
        if (user.Status != UserStatus.ACTIVE || await periods.GetActiveAsync(cancellationToken) is null)
            return Result<ParkingAccessView>.Success(new(summary, null, []));
        var owned = await vehicles.GetByCurrentOwnerAsync(user.Id, cancellationToken);
        var eligible = owned.Where(x => x.Status == VehicleStatus.ACTIVE && x.RegistrationState == RegistrationState.ACTIVE && !x.IsInside &&
            !(user.UserType == InstitutionalUserType.STUDENT && x.Type == VehicleType.CAR)).ToArray();
        var enabled = eligible.Where(x => x.VerificationImageId.HasValue && x.VerificationImageType == VehicleVerificationImage.ForVehicle(x.Type)).Select(Vehicle).ToArray();
        var missing = enabled.Length == 0 && eligible.Length > 0;
        return Result<ParkingAccessView>.Success(new(summary, null, enabled, EntryBlockCode: missing ? "VEHICLE_VERIFICATION_REQUIRED" : null,
            EntryBlockMessage: missing ? "El vehículo no tiene evidencia de verificación registrada." : null));
    }
    internal static EligibleVehicleView Vehicle(VehicleView x) => new(x.Id, x.Type, x.Plate ?? x.FrameNumber!, x.Brand, x.Model, x.Color,
        x.VerificationImageId is { } id && x.VerificationImageType is { } type && type == VehicleVerificationImage.ForVehicle(x.Type) ? new(id, type, $"/api/v1/vehicles/{x.Id}/verification-image/content") : null);
}
public sealed class GetParkingMovementQueryHandler(AdministrationOperationContext operation, IParkingMovementReadRepository reads, IClock clock)
    : IRequestHandler<GetParkingMovementQuery, Result<ParkingMovementView>>
{
    public async Task<Result<ParkingMovementView>> Handle(GetParkingMovementQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<ParkingMovementView>.Failure(permission);
        var movement = await reads.GetByIdAsync(request.MovementId, clock.UtcNow, cancellationToken);
        return movement is null ? Result<ParkingMovementView>.Failure(new("PARKING_MOVEMENT_NOT_FOUND", "El movimiento no existe.", ErrorType.NotFound)) : Result<ParkingMovementView>.Success(movement);
    }
}
public sealed class GetParkingMovementVehicleQueryHandler(AdministrationOperationContext operation, IParkingMovementRepository movements, IVehicleRepository vehicles)
    : IRequestHandler<GetParkingMovementVehicleQuery, Result<EligibleVehicleView>>
{
    public async Task<Result<EligibleVehicleView>> Handle(GetParkingMovementVehicleQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<EligibleVehicleView>.Failure(permission);
        var movement = await movements.GetByIdAsync(request.MovementId, cancellationToken);
        var vehicle = movement is null ? null : await vehicles.GetViewAsync(movement.VehicleId, cancellationToken);
        return vehicle is null ? Result<EligibleVehicleView>.Failure(VehicleErrors.NotFound) : Result<EligibleVehicleView>.Success(GetParkingAccessUserQueryHandler.Vehicle(vehicle));
    }
}
public sealed class GetVehiclesInsideQueryHandler(AdministrationOperationContext operation, IParkingMovementReadRepository reads, IClock clock)
    : IRequestHandler<GetVehiclesInsideQuery, Result<VehiclesInsideView>>
{
    public async Task<Result<VehiclesInsideView>> Handle(GetVehiclesInsideQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<VehiclesInsideView>.Failure(permission);
        return Result<VehiclesInsideView>.Success(await reads.GetInsideAsync(new(ParkingLotId: request.ParkingLotId,
            VehicleType: request.VehicleType, Search: request.Search, Page: request.Page, PageSize: request.PageSize), clock.UtcNow, cancellationToken));
    }
}
public sealed class GetParkingMovementsQueryHandler(AdministrationOperationContext operation, IParkingMovementReadRepository reads, IParkingTimeZone timeZone, IClock clock)
    : IRequestHandler<GetParkingMovementsQuery, Result<PagedResult<ParkingMovementView>>>
{
    public async Task<Result<PagedResult<ParkingMovementView>>> Handle(GetParkingMovementsQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<PagedResult<ParkingMovementView>>.Failure(permission);
        return Result<PagedResult<ParkingMovementView>>.Success(await reads.SearchAsync(new(ParkingLotId: request.ParkingLotId,
            VehicleType: request.VehicleType, Status: request.Status, IdentificationNumber: request.IdentificationNumber,
            Plate: request.Plate, FrameNumber: request.FrameNumber,
            FromUtc: request.DateFrom.HasValue ? timeZone.GetUtcStartOfDay(request.DateFrom.Value) : null,
            UntilUtc: ParkingDates.EndExclusive(request.DateTo, timeZone), Page: request.Page, PageSize: request.PageSize), clock.UtcNow, cancellationToken));
    }
}
public sealed class GetMyParkingHistoryQueryHandler(AdministrationOperationContext operation, ICurrentUser actor,
    IParkingMovementReadRepository reads, IParkingTimeZone timeZone, IClock clock)
    : IRequestHandler<GetMyParkingHistoryQuery, Result<PagedResult<ParkingMovementView>>>
{
    public async Task<Result<PagedResult<ParkingMovementView>>> Handle(GetMyParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([], cancellationToken) is { } permission) return Result<PagedResult<ParkingMovementView>>.Failure(permission);
        return Result<PagedResult<ParkingMovementView>>.Success(await reads.SearchAsync(new(UserId: actor.UserId,
            VehicleId: request.VehicleId, FromUtc: request.DateFrom.HasValue ? timeZone.GetUtcStartOfDay(request.DateFrom.Value) : null,
            UntilUtc: ParkingDates.EndExclusive(request.DateTo, timeZone), Page: request.Page, PageSize: request.PageSize), clock.UtcNow, cancellationToken));
    }
}
internal static class ParkingDates
{
    public static DateTimeOffset? EndExclusive(DateOnly? day, IParkingTimeZone timeZone) =>
        day.HasValue && day.Value != DateOnly.MaxValue ? timeZone.GetUtcStartOfDay(day.Value.AddDays(1)) : null;
}
