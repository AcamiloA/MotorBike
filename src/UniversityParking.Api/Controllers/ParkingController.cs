using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Api.RateLimiting;
using UniversityParking.Application.Parking;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking")]
[Authorize(Policy = PolicyNames.Authenticated)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
[ProducesResponseType(typeof(ApiProblemDetails), 409)]
[ProducesResponseType(typeof(ApiProblemDetails), 429)]
public sealed class ParkingController(ISender sender) : ControllerBase
{
    [HttpPost("access/lookup")]
    [Authorize(Policy = PolicyNames.GuardOrAdmin)]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    [ProducesResponseType(typeof(ParkingAccessResponse), 200)]
    public async Task<IActionResult> Lookup(ParkingAccessRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetParkingAccessUserQuery(request.CardCode, request.IdentificationNumber), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var value = result.Value;
        return Ok(new ParkingAccessResponse(new(value.User.Id, value.User.FullName, value.User.MemberType.ToString(), value.User.Status.ToString()),
            value.CurrentMovement is null ? null : Map(value.CurrentMovement),
            value.EligibleVehicles.Select(x => new EligibleVehicleResponse(x.Id, x.Type.ToString(), x.Identifier, x.Brand, x.Model, x.Color)).ToArray()));
    }
    [HttpPost("check-in")]
    [Authorize(Policy = PolicyNames.Guard)]
    [EnableRateLimiting(RateLimitPolicies.ParkingCommands)]
    [ProducesResponseType(typeof(ParkingMovementResponse), 201)]
    public async Task<IActionResult> CheckIn(CheckInVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CheckInVehicleCommand(request.UserId, request.VehicleId, request.ParkingLotId), cancellationToken);
        return result.IsSuccess ? Created("/api/v1/parking/movements", Map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("check-out")]
    [Authorize(Policy = PolicyNames.Guard)]
    [EnableRateLimiting(RateLimitPolicies.ParkingCommands)]
    [ProducesResponseType(typeof(ParkingMovementResponse), 200)]
    public async Task<IActionResult> CheckOut(CheckOutVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CheckOutVehicleCommand(request.VehicleId), cancellationToken);
        return result.IsSuccess ? Ok(Map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("inside")]
    [Authorize(Policy = PolicyNames.GuardOrAdmin)]
    [ProducesResponseType(typeof(VehiclesInsideResponse), 200)]
    public async Task<IActionResult> Inside(CancellationToken cancellationToken, [FromQuery] Guid? parkingLotId = null,
        [FromQuery] ParkingVehicleType? vehicleType = null, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetVehiclesInsideQuery(parkingLotId, vehicleType.HasValue ? (VehicleType)vehicleType.Value : null, search, page, pageSize), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var value = result.Value;
        return Ok(new VehiclesInsideResponse(value.Movements.Items.Select(Map).ToArray(), value.Movements.Page, value.Movements.PageSize,
            value.Movements.TotalCount, value.Movements.TotalPages, new(value.Counts.Total, value.Counts.Cars, value.Counts.Motorcycles, value.Counts.Bicycles)));
    }
    [HttpGet("movements")]
    [Authorize(Policy = PolicyNames.GuardOrAdmin)]
    [ProducesResponseType(typeof(PagedResponse<ParkingMovementResponse>), 200)]
    public async Task<IActionResult> Movements(CancellationToken cancellationToken, [FromQuery] Guid? parkingLotId = null,
        [FromQuery] string? identificationNumber = null, [FromQuery] string? plate = null, [FromQuery] string? frameNumber = null,
        [FromQuery] ParkingVehicleType? vehicleType = null, [FromQuery] ParkingMovementState? status = null,
        [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetParkingMovementsQuery(parkingLotId, identificationNumber, plate, frameNumber,
            vehicleType.HasValue ? (VehicleType)vehicleType.Value : null, status.HasValue ? (ParkingMovementStatus)status.Value : null, dateFrom, dateTo, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(MapPage(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("history/me")]
    [ProducesResponseType(typeof(PagedResponse<ParkingMovementResponse>), 200)]
    public async Task<IActionResult> MyHistory(CancellationToken cancellationToken, [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null, [FromQuery] Guid? vehicleId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (Request.Query.ContainsKey("userId")) return ProblemResponses.Map(HttpContext, CommonErrors.Validation(
            new Dictionary<string, IReadOnlyList<string>> { ["userId"] = ["El historial personal obtiene el usuario de la sesión."] }));
        var result = await sender.Send(new GetMyParkingHistoryQuery(dateFrom, dateTo, vehicleId, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(MapPage(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    private static PagedResponse<ParkingMovementResponse> MapPage(PagedResult<ParkingMovementView> page) =>
        new(page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    private static ParkingMovementResponse Map(ParkingMovementView x) => new(x.Id, x.UserId, x.UserFullName, x.VehicleId, x.VehicleType.ToString(),
        x.VehicleIdentifier, x.ParkingLotId, x.ParkingLotName, x.ParkingZoneId, x.ParkingZoneName, x.CheckInAtUtc, x.CheckInGuardId,
        x.CheckOutAtUtc, x.CheckOutGuardId, x.Status.ToString(), x.Duration);
}
