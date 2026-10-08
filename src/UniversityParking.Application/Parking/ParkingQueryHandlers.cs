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
    IAcademicPeriodRepository periods, IClock clock) : IRequestHandler<GetParkingAccessUserQuery, Result<ParkingAccessView>>
{
    public async Task<Result<ParkingAccessView>> Handle(GetParkingAccessUserQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<ParkingAccessView>.Failure(permission);
        var user = !string.IsNullOrWhiteSpace(request.CardCode) ? await users.GetByCardCodeAsync(new CardCode(request.CardCode), cancellationToken) :
            await users.GetByIdentificationNumberAsync(new IdentificationNumber(request.IdentificationNumber!), cancellationToken);
        if (user is null) return Result<ParkingAccessView>.Failure(UserErrors.NotFound);
        var summary = new ParkingAccessUserView(user.Id, user.FullName, user.MemberType, user.Status);
        var open = await movements.GetOpenByUserIdAsync(user.Id, cancellationToken);
        if (open is not null) return Result<ParkingAccessView>.Success(new(summary, await reads.GetByIdAsync(open.Id, clock.UtcNow, cancellationToken), []));
        if (user.Status != UserStatus.ACTIVE || await periods.GetActiveAsync(cancellationToken) is null)
            return Result<ParkingAccessView>.Success(new(summary, null, []));
        var owned = await vehicles.GetByCurrentOwnerAsync(user.Id, cancellationToken);
        var eligible = owned.Where(x => x.Status == VehicleStatus.ACTIVE && x.RegistrationState == RegistrationState.ACTIVE && !x.IsInside &&
            !(user.MemberType == MemberType.STUDENT && x.Type == VehicleType.CAR))
            .Select(x => new EligibleVehicleView(x.Id, x.Type, x.Plate ?? x.FrameNumber!, x.Brand, x.Model, x.Color)).ToArray();
        return Result<ParkingAccessView>.Success(new(summary, null, eligible));
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
