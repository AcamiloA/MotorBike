using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.Parking;

public enum ParkingVehicleType { CAR, MOTORCYCLE, BICYCLE, SCOOTER }
public enum ParkingMovementState { OPEN, CLOSED }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParkingAccessRequest(string? QrPayload = null, string? IdentificationNumber = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckInVehicleRequest([property: JsonRequired] Guid UserId, [property: JsonRequired] Guid VehicleId, [property: JsonRequired] Guid ParkingLotId, Guid? MovementId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckOutVehicleRequest([property: JsonRequired] Guid MovementId, [property: JsonRequired] Guid VehicleId);
public sealed record ParkingAccessUserResponse(Guid Id, string FullName, string MemberType, string Status);
public sealed record ParkingVerificationImageResponse(Guid Id, string Type, string ContentUrl);
public sealed record EligibleVehicleResponse(Guid Id, string Type, string Identifier, string Brand, string Model, string Color, ParkingVerificationImageResponse? VerificationImage = null);
public sealed record ParkingMovementResponse(Guid MovementId, Guid UserId, string UserFullName, Guid VehicleId, string VehicleType,
    string VehicleIdentifier, Guid ParkingLotId, string ParkingLotName, Guid ParkingZoneId, string ParkingZoneName,
    DateTimeOffset CheckInAtUtc, Guid CheckInGuardId, DateTimeOffset? CheckOutAtUtc, Guid? CheckOutGuardId, string Status, TimeSpan Duration);
public sealed record ParkingAccessResponse(ParkingAccessUserResponse User, ParkingMovementResponse? CurrentMovement, IReadOnlyList<EligibleVehicleResponse> EligibleVehicles,
    EligibleVehicleResponse? CurrentVehicle = null, string? EntryBlockCode = null, string? EntryBlockMessage = null);
public sealed record ParkingInsideCountsResponse(long Total, long Cars, long Motorcycles, long Bicycles);
public sealed record VehiclesInsideResponse(IReadOnlyList<ParkingMovementResponse> Items, int Page, int PageSize, long TotalCount,
    long TotalPages, ParkingInsideCountsResponse Counts);
