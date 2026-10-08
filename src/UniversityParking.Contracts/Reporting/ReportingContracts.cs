using UniversityParking.Contracts.Common;

namespace UniversityParking.Contracts.Reporting;

public sealed record AuditResponse(Guid Id, Guid? ActorUserId, string Action, string EntityType, Guid? EntityId,
    string? OldValues, string? NewValues, string? IpAddress, string? TraceId, DateTimeOffset CreatedAt);
public sealed record NamedReferenceResponse(Guid Id, string Name);
public sealed record AdminDashboardResponse(long ActiveUsers, long ActiveVehicles, long VehiclesInside, long TodayCheckIns,
    long TodayCheckOuts, long OpenIncidents, NamedReferenceResponse? CurrentAcademicPeriod);
public sealed record GuardDashboardResponse(NamedReferenceResponse ParkingLot, long VehiclesInside, long TodayCheckIns, long TodayCheckOuts, long OpenIncidents);
public sealed record DailyAccessResponse(DateOnly Date, long CheckIns, long CheckOuts);
public sealed record AccessGroupResponse(string Type, long CheckIns, long CheckOuts);
public sealed record OwnershipHistoryResponse(Guid Id, Guid VehicleId, Guid UserId, DateTimeOffset StartAt, DateTimeOffset? EndAt, string? TransferReason);
public sealed record RegistrationHistoryResponse(Guid Id, Guid VehicleId, Guid UserId, Guid AcademicPeriodId, string Status, DateTimeOffset RegisteredAt, DateTimeOffset? CancelledAt);
public sealed record MovementHistoryResponse(Guid Id, Guid UserId, Guid VehicleId, Guid ParkingLotId, Guid ParkingZoneId, DateTimeOffset CheckInAt,
    Guid CheckInGuardId, DateTimeOffset? CheckOutAt, Guid? CheckOutGuardId, string Status);
public sealed record IncidentHistoryResponse(Guid Id, Guid? UserId, Guid? VehicleId, Guid? ParkingMovementId, Guid ParkingLotId,
    string Type, string Status, string Description, Guid ReportedBy, DateTimeOffset OccurredAt);
public sealed record VehicleHistoryResponse(Guid VehicleId, PagedResponse<OwnershipHistoryResponse> Ownerships, PagedResponse<RegistrationHistoryResponse> Registrations,
    PagedResponse<MovementHistoryResponse> Movements, PagedResponse<IncidentHistoryResponse> Incidents);
public sealed record UserHistoryResponse(Guid UserId, PagedResponse<OwnershipHistoryResponse> Ownerships, PagedResponse<MovementHistoryResponse> Movements, PagedResponse<IncidentHistoryResponse> Incidents);
public sealed record GuardActivityResponse(Guid GuardUserId, long CheckIns, long CheckOuts, long IncidentsReported);
