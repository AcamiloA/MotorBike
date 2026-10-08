using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Reporting;

public static class ReportingDates
{
    public static UtcRange Range(DateOnly from, DateOnly to, IParkingTimeZone zone) =>
        new(zone.GetUtcStartOfDay(from), End(to, zone));
    public static DateTimeOffset? End(DateOnly? to, IParkingTimeZone zone) => to.HasValue && to.Value != DateOnly.MaxValue ? zone.GetUtcStartOfDay(to.Value.AddDays(1)) : null;
    public static UtcRange Today(IClock clock, IParkingTimeZone zone)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")).DateTime);
        return Range(day, day, zone);
    }
}
public sealed class ReportingHandlers(AdministrationOperationContext operation, IReportingRepository reads, IParkingTimeZone zone, IClock clock) :
    IRequestHandler<GetAuditLogsQuery, Result<PagedResult<AuditView>>>, IRequestHandler<GetAdminDashboardQuery, Result<AdminDashboardView>>,
    IRequestHandler<GetGuardDashboardQuery, Result<GuardDashboardView>>, IRequestHandler<GetDailyAccessReportQuery, Result<IReadOnlyList<DailyAccessView>>>,
    IRequestHandler<GetAccessByVehicleTypeReportQuery, Result<IReadOnlyList<AccessGroupView>>>, IRequestHandler<GetAccessByMemberTypeReportQuery, Result<IReadOnlyList<AccessGroupView>>>,
    IRequestHandler<GetVehicleHistoryReportQuery, Result<VehicleHistoryView>>, IRequestHandler<GetUserHistoryReportQuery, Result<UserHistoryView>>,
    IRequestHandler<GetGuardActivityReportQuery, Result<GuardActivityView>>
{
    public async Task<Result<PagedResult<AuditView>>> Handle(GetAuditLogsQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<PagedResult<AuditView>>.Failure(error);
        return Result<PagedResult<AuditView>>.Success(await reads.GetAuditAsync(new(request.ActorUserId, request.Action, request.EntityType, request.EntityId,
            request.DateFrom.HasValue ? zone.GetUtcStartOfDay(request.DateFrom.Value) : null, ReportingDates.End(request.DateTo, zone), request.Page, request.PageSize), token));
    }
    public async Task<Result<AdminDashboardView>> Handle(GetAdminDashboardQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<AdminDashboardView>.Failure(error);
        return Result<AdminDashboardView>.Success(await reads.GetAdminDashboardAsync(ReportingDates.Today(clock, zone), token));
    }
    public async Task<Result<GuardDashboardView>> Handle(GetGuardDashboardQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard], token) is { } error) return Result<GuardDashboardView>.Failure(error);
        var value = await reads.GetGuardDashboardAsync(request.ParkingLotId, ReportingDates.Today(clock, zone), token);
        return value is null ? Result<GuardDashboardView>.Failure(ParkingErrors.LotNotFound) : Result<GuardDashboardView>.Success(value);
    }
    public async Task<Result<IReadOnlyList<DailyAccessView>>> Handle(GetDailyAccessReportQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<IReadOnlyList<DailyAccessView>>.Failure(error);
        return Result<IReadOnlyList<DailyAccessView>>.Success(await reads.GetDailyAccessAsync(ReportingDates.Range(request.DateFrom, request.DateTo, zone), request.ParkingLotId, token));
    }
    public async Task<Result<IReadOnlyList<AccessGroupView>>> Handle(GetAccessByVehicleTypeReportQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<IReadOnlyList<AccessGroupView>>.Failure(error);
        return Result<IReadOnlyList<AccessGroupView>>.Success(await reads.GetByVehicleTypeAsync(ReportingDates.Range(request.DateFrom, request.DateTo, zone), request.ParkingLotId, token));
    }
    public async Task<Result<IReadOnlyList<AccessGroupView>>> Handle(GetAccessByMemberTypeReportQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<IReadOnlyList<AccessGroupView>>.Failure(error);
        return Result<IReadOnlyList<AccessGroupView>>.Success(await reads.GetByMemberTypeAsync(ReportingDates.Range(request.DateFrom, request.DateTo, zone), request.ParkingLotId, token));
    }
    public async Task<Result<VehicleHistoryView>> Handle(GetVehicleHistoryReportQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<VehicleHistoryView>.Failure(error);
        var value = await reads.GetVehicleHistoryAsync(request.VehicleId, request.Page, request.PageSize, token);
        return value is null ? Result<VehicleHistoryView>.Failure(VehicleErrors.NotFound) : Result<VehicleHistoryView>.Success(value);
    }
    public async Task<Result<UserHistoryView>> Handle(GetUserHistoryReportQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<UserHistoryView>.Failure(error);
        var value = await reads.GetUserHistoryAsync(request.UserId, request.Page, request.PageSize, token);
        return value is null ? Result<UserHistoryView>.Failure(UserErrors.NotFound) : Result<UserHistoryView>.Success(value);
    }
    public async Task<Result<GuardActivityView>> Handle(GetGuardActivityReportQuery request, CancellationToken token)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], token) is { } error) return Result<GuardActivityView>.Failure(error);
        var value = await reads.GetGuardActivityAsync(request.GuardUserId, ReportingDates.Range(request.DateFrom, request.DateTo, zone), token);
        return value is null ? Result<GuardActivityView>.Failure(UserErrors.NotFound) : Result<GuardActivityView>.Success(value);
    }
}
