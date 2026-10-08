using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Reporting;

namespace UniversityParking.Application.Common.Abstractions;

public interface IReportingRepository
{
    Task<PagedResult<AuditView>> GetAuditAsync(AuditFilter filter, CancellationToken token);
    Task<AdminDashboardView> GetAdminDashboardAsync(UtcRange today, CancellationToken token);
    Task<GuardDashboardView?> GetGuardDashboardAsync(Guid lotId, UtcRange today, CancellationToken token);
    Task<IReadOnlyList<DailyAccessView>> GetDailyAccessAsync(UtcRange range, Guid? lotId, CancellationToken token);
    Task<IReadOnlyList<AccessGroupView>> GetByVehicleTypeAsync(UtcRange range, Guid? lotId, CancellationToken token);
    Task<IReadOnlyList<AccessGroupView>> GetByMemberTypeAsync(UtcRange range, Guid? lotId, CancellationToken token);
    Task<VehicleHistoryView?> GetVehicleHistoryAsync(Guid id, int page, int size, CancellationToken token);
    Task<UserHistoryView?> GetUserHistoryAsync(Guid id, int page, int size, CancellationToken token);
    Task<GuardActivityView?> GetGuardActivityAsync(Guid id, UtcRange range, CancellationToken token);
}
