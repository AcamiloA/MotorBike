using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Reporting;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Reporting;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Policy = PolicyNames.Authenticated)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
public sealed class ReportingController(ISender sender) : ControllerBase
{
    [HttpGet("audit-logs")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(PagedResponse<AuditResponse>), 200)]
    public async Task<IActionResult> Audit(CancellationToken token, [FromQuery] Guid? actorUserId = null, [FromQuery] string? action = null,
        [FromQuery] string? entityType = null, [FromQuery] Guid? entityId = null, [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Respond(await sender.Send(new GetAuditLogsQuery(actorUserId, action, entityType, entityId, dateFrom, dateTo, page, pageSize), token),
            x => Page(x, a => new AuditResponse(a.Id, a.ActorUserId, a.Action, a.EntityType, a.EntityId, a.OldValues, a.NewValues, a.IpAddress, a.TraceId, a.CreatedAt)));
    [HttpGet("dashboard/admin")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(AdminDashboardResponse), 200)]
    public async Task<IActionResult> AdminDashboard(CancellationToken token) => Respond(await sender.Send(new GetAdminDashboardQuery(), token),
        x => new AdminDashboardResponse(x.ActiveUsers, x.ActiveVehicles, x.VehiclesInside, x.TodayCheckIns, x.TodayCheckOuts, x.OpenIncidents,
            x.CurrentAcademicPeriod is { } period ? new(period.Id, period.Name) : null));
    [HttpGet("dashboard/guard")]
    [Authorize(Policy = PolicyNames.Guard)]
    [ProducesResponseType(typeof(GuardDashboardResponse), 200)]
    public async Task<IActionResult> GuardDashboard([FromQuery] Guid parkingLotId, CancellationToken token) => Respond(await sender.Send(new GetGuardDashboardQuery(parkingLotId), token),
        x => new GuardDashboardResponse(new(x.ParkingLot.Id, x.ParkingLot.Name), x.VehiclesInside, x.TodayCheckIns, x.TodayCheckOuts, x.OpenIncidents));
    [HttpGet("reports/access/daily")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(DailyAccessResponse[]), 200)]
    public async Task<IActionResult> Daily([FromQuery] DateOnly dateFrom, [FromQuery] DateOnly dateTo, CancellationToken token, [FromQuery] Guid? parkingLotId = null) =>
        Respond(await sender.Send(new GetDailyAccessReportQuery(dateFrom, dateTo, parkingLotId), token), x => x.Select(a => new DailyAccessResponse(a.Date, a.CheckIns, a.CheckOuts)).ToArray());
    [HttpGet("reports/access/by-vehicle-type")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(AccessGroupResponse[]), 200)]
    public async Task<IActionResult> VehicleType([FromQuery] DateOnly dateFrom, [FromQuery] DateOnly dateTo, CancellationToken token, [FromQuery] Guid? parkingLotId = null) =>
        Respond(await sender.Send(new GetAccessByVehicleTypeReportQuery(dateFrom, dateTo, parkingLotId), token), x => x.Select(a => new AccessGroupResponse(a.Type, a.CheckIns, a.CheckOuts)).ToArray());
    [HttpGet("reports/access/by-member-type")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(AccessGroupResponse[]), 200)]
    public async Task<IActionResult> MemberType([FromQuery] DateOnly dateFrom, [FromQuery] DateOnly dateTo, CancellationToken token, [FromQuery] Guid? parkingLotId = null) =>
        Respond(await sender.Send(new GetAccessByMemberTypeReportQuery(dateFrom, dateTo, parkingLotId), token), x => x.Select(a => new AccessGroupResponse(a.Type, a.CheckIns, a.CheckOuts)).ToArray());
    [HttpGet("reports/vehicles/{id:guid}/history")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(VehicleHistoryResponse), 200)]
    public async Task<IActionResult> VehicleHistory(Guid id, CancellationToken token, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Respond(await sender.Send(new GetVehicleHistoryReportQuery(id, page, pageSize), token), x => new VehicleHistoryResponse(x.VehicleId,
            Page(x.Ownerships, Ownership), Page(x.Registrations, a => new RegistrationHistoryResponse(a.Id, a.VehicleId, a.UserId, a.AcademicPeriodId, a.Status, a.RegisteredAt, a.CancelledAt)),
            Page(x.Movements, Movement), Page(x.Incidents, Incident)));
    [HttpGet("reports/users/{id:guid}/history")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(UserHistoryResponse), 200)]
    public async Task<IActionResult> UserHistory(Guid id, CancellationToken token, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Respond(await sender.Send(new GetUserHistoryReportQuery(id, page, pageSize), token), x => new UserHistoryResponse(x.UserId, Page(x.Ownerships, Ownership), Page(x.Movements, Movement), Page(x.Incidents, Incident)));
    [HttpGet("reports/guards/{id:guid}/activity")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(GuardActivityResponse), 200)]
    public async Task<IActionResult> GuardActivity(Guid id, [FromQuery] DateOnly dateFrom, [FromQuery] DateOnly dateTo, CancellationToken token) =>
        Respond(await sender.Send(new GetGuardActivityReportQuery(id, dateFrom, dateTo), token), x => new GuardActivityResponse(x.GuardUserId, x.CheckIns, x.CheckOuts, x.IncidentsReported));
    private IActionResult Respond<T, TResponse>(Result<T> result, Func<T, TResponse> map) => result.IsSuccess ? Ok(map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    private static PagedResponse<TResponse> Page<T, TResponse>(PagedResult<T> page, Func<T, TResponse> map) => new(page.Items.Select(map).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    private static OwnershipHistoryResponse Ownership(OwnershipHistoryView a) => new(a.Id, a.VehicleId, a.UserId, a.StartAt, a.EndAt, a.TransferReason);
    private static MovementHistoryResponse Movement(MovementHistoryView a) => new(a.Id, a.UserId, a.VehicleId, a.ParkingLotId, a.ParkingZoneId, a.CheckInAt, a.CheckInGuardId, a.CheckOutAt, a.CheckOutGuardId, a.Status);
    private static IncidentHistoryResponse Incident(IncidentHistoryView a) => new(a.Id, a.UserId, a.VehicleId, a.ParkingMovementId, a.ParkingLotId, a.Type, a.Status, a.Description, a.ReportedBy, a.OccurredAt);
}
