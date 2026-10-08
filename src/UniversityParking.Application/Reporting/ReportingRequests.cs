using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Reporting;

public sealed record GetAuditLogsQuery(Guid? ActorUserId = null, string? Action = null, string? EntityType = null, Guid? EntityId = null,
    DateOnly? DateFrom = null, DateOnly? DateTo = null, int Page = 1, int PageSize = 20) : IQuery<PagedResult<AuditView>>;
public sealed record GetAdminDashboardQuery : IQuery<AdminDashboardView>;
public sealed record GetGuardDashboardQuery(Guid ParkingLotId) : IQuery<GuardDashboardView>;
public interface IAccessRange { DateOnly DateFrom { get; } DateOnly DateTo { get; } Guid? ParkingLotId { get; } }
public sealed record GetDailyAccessReportQuery(DateOnly DateFrom, DateOnly DateTo, Guid? ParkingLotId = null) : IQuery<IReadOnlyList<DailyAccessView>>, IAccessRange;
public sealed record GetAccessByVehicleTypeReportQuery(DateOnly DateFrom, DateOnly DateTo, Guid? ParkingLotId = null) : IQuery<IReadOnlyList<AccessGroupView>>, IAccessRange;
public sealed record GetAccessByMemberTypeReportQuery(DateOnly DateFrom, DateOnly DateTo, Guid? ParkingLotId = null) : IQuery<IReadOnlyList<AccessGroupView>>, IAccessRange;
public sealed record GetVehicleHistoryReportQuery(Guid VehicleId, int Page = 1, int PageSize = 20) : IQuery<VehicleHistoryView>;
public sealed record GetUserHistoryReportQuery(Guid UserId, int Page = 1, int PageSize = 20) : IQuery<UserHistoryView>;
public sealed record GetGuardActivityReportQuery(Guid GuardUserId, DateOnly DateFrom, DateOnly DateTo) : IQuery<GuardActivityView>;
public sealed record AuditView(Guid Id, Guid? ActorUserId, string Action, string EntityType, Guid? EntityId,
    string? OldValues, string? NewValues, string? IpAddress, string? TraceId, DateTimeOffset CreatedAt);
public sealed record NamedReference(Guid Id, string Name);
public sealed record AdminDashboardView(long ActiveUsers, long ActiveVehicles, long VehiclesInside, long TodayCheckIns,
    long TodayCheckOuts, long OpenIncidents, NamedReference? CurrentAcademicPeriod);
public sealed record GuardDashboardView(NamedReference ParkingLot, long VehiclesInside, long TodayCheckIns, long TodayCheckOuts, long OpenIncidents);
public sealed record DailyAccessView(DateOnly Date, long CheckIns, long CheckOuts);
public sealed record AccessGroupView(string Type, long CheckIns, long CheckOuts);
public sealed record OwnershipHistoryView(Guid Id, Guid VehicleId, Guid UserId, DateTimeOffset StartAt, DateTimeOffset? EndAt, string? TransferReason);
public sealed record RegistrationHistoryView(Guid Id, Guid VehicleId, Guid UserId, Guid AcademicPeriodId, string Status, DateTimeOffset RegisteredAt, DateTimeOffset? CancelledAt);
public sealed record MovementHistoryView(Guid Id, Guid UserId, Guid VehicleId, Guid ParkingLotId, Guid ParkingZoneId, DateTimeOffset CheckInAt,
    Guid CheckInGuardId, DateTimeOffset? CheckOutAt, Guid? CheckOutGuardId, string Status);
public sealed record IncidentHistoryView(Guid Id, Guid? UserId, Guid? VehicleId, Guid? ParkingMovementId, Guid ParkingLotId,
    string Type, string Status, string Description, Guid ReportedBy, DateTimeOffset OccurredAt);
public sealed record VehicleHistoryView(Guid VehicleId, PagedResult<OwnershipHistoryView> Ownerships, PagedResult<RegistrationHistoryView> Registrations,
    PagedResult<MovementHistoryView> Movements, PagedResult<IncidentHistoryView> Incidents);
public sealed record UserHistoryView(Guid UserId, PagedResult<OwnershipHistoryView> Ownerships, PagedResult<MovementHistoryView> Movements, PagedResult<IncidentHistoryView> Incidents);
public sealed record GuardActivityView(Guid GuardUserId, long CheckIns, long CheckOuts, long IncidentsReported);
public sealed record UtcRange(DateTimeOffset From, DateTimeOffset? Until);
public sealed record AuditFilter(Guid? ActorUserId, string? Action, string? EntityType, Guid? EntityId, DateTimeOffset? From, DateTimeOffset? Until, int Page, int PageSize);
