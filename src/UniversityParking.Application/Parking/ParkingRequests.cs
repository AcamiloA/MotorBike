using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Parking;

public sealed record GetParkingAccessUserQuery(string? QrPayload, string? IdentificationNumber) : IQuery<ParkingAccessView>;
public sealed record CheckInVehicleCommand(Guid UserId, Guid VehicleId, Guid ParkingLotId, Guid? MovementId = null) : ICommand<ParkingMovementView>;
public sealed record CheckOutVehicleCommand(Guid MovementId, Guid VehicleId) : ICommand<ParkingMovementView>;
public sealed record GetParkingMovementQuery(Guid MovementId) : IQuery<ParkingMovementView>;
public sealed record GetParkingMovementVehicleQuery(Guid MovementId) : IQuery<EligibleVehicleView>;
public interface IParkingPage { int Page { get; } int PageSize { get; } }
public sealed record GetVehiclesInsideQuery(Guid? ParkingLotId = null, VehicleType? VehicleType = null, string? Search = null,
    int Page = 1, int PageSize = 20) : IQuery<VehiclesInsideView>, IParkingPage;
public sealed record GetParkingMovementsQuery(Guid? ParkingLotId = null, string? IdentificationNumber = null,
    string? Plate = null, string? FrameNumber = null, VehicleType? VehicleType = null, ParkingMovementStatus? Status = null,
    DateOnly? DateFrom = null, DateOnly? DateTo = null, int Page = 1, int PageSize = 20) : IQuery<PagedResult<ParkingMovementView>>, IParkingPage;
public sealed record GetMyParkingHistoryQuery(DateOnly? DateFrom = null, DateOnly? DateTo = null,
    Guid? VehicleId = null, int Page = 1, int PageSize = 20) : IQuery<PagedResult<ParkingMovementView>>, IParkingPage;
public sealed record ParkingAccessUserView(Guid Id, string FullName, MemberType MemberType, UserStatus Status);
public sealed record ParkingVerificationImageView(Guid Id, VehicleVerificationImageType Type, string ContentUrl);
public sealed record EligibleVehicleView(Guid Id, VehicleType Type, string Identifier, string Brand, string Model, string Color, ParkingVerificationImageView? VerificationImage = null);
public sealed record ParkingAccessView(ParkingAccessUserView User, ParkingMovementView? CurrentMovement, IReadOnlyList<EligibleVehicleView> EligibleVehicles,
    EligibleVehicleView? CurrentVehicle = null, string? EntryBlockCode = null, string? EntryBlockMessage = null);
public sealed record ParkingMovementView(Guid Id, Guid UserId, string UserFullName, Guid VehicleId, VehicleType VehicleType,
    string VehicleIdentifier, Guid ParkingLotId, string ParkingLotName, Guid ParkingZoneId, string ParkingZoneName,
    DateTimeOffset CheckInAtUtc, Guid CheckInGuardId, DateTimeOffset? CheckOutAtUtc, Guid? CheckOutGuardId,
    ParkingMovementStatus Status, TimeSpan Duration);
public sealed record InsideCounts(long Total, long Cars, long Motorcycles, long Bicycles);
public sealed record VehiclesInsideView(PagedResult<ParkingMovementView> Movements, InsideCounts Counts);
public sealed record ParkingMovementFilter(Guid? UserId = null, Guid? VehicleId = null, Guid? ParkingLotId = null,
    VehicleType? VehicleType = null, ParkingMovementStatus? Status = null, string? Search = null,
    string? IdentificationNumber = null, string? Plate = null, string? FrameNumber = null,
    DateTimeOffset? FromUtc = null, DateTimeOffset? UntilUtc = null, int Page = 1, int PageSize = 20);
