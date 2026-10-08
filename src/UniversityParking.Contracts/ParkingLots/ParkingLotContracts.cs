using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.ParkingLots;

public enum ParkingLotState { ACTIVE, INACTIVE }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateParkingLotRequest(string Name, string Campus, [property: JsonRequired] TimeOnly OpeningTime, [property: JsonRequired] TimeOnly ClosingTime);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateParkingLotRequest(string Name, string Campus, [property: JsonRequired] TimeOnly OpeningTime, [property: JsonRequired] TimeOnly ClosingTime);
public sealed record ParkingLotCreatedResponse(Guid Id);
public sealed record ParkingZoneResponse(Guid Id, string Name, string VehicleType, string Status);
public sealed record ParkingLotResponse(Guid Id, string Name, string Campus, TimeOnly OpeningTime, TimeOnly ClosingTime,
    string Status, IReadOnlyList<ParkingZoneResponse> Zones);
