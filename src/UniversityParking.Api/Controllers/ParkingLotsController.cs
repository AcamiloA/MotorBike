using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Application.ParkingLots;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Common;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-lots")]
[Authorize(Policy = PolicyNames.GuardOrAdmin)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
[ProducesResponseType(typeof(ApiProblemDetails), 409)]
public sealed class ParkingLotsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ParkingLotResponse>), 200)]
    public async Task<IActionResult> GetLots(CancellationToken cancellationToken, [FromQuery] ParkingLotState? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = User.IsInRole(RoleCodes.Admin) ? await sender.Send(new GetParkingLotsQuery(status.HasValue ? (ParkingLotStatus)status.Value : null, page, pageSize), cancellationToken) :
            await sender.Send(new GetActiveParkingLotsQuery(page, pageSize), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var value = result.Value;
        return Ok(new PagedResponse<ParkingLotResponse>(value.Items.Select(x => new ParkingLotResponse(x.Id, x.Name, x.Campus,
            x.OpeningTime, x.ClosingTime, x.Status.ToString(), x.Zones.Select(z => new ParkingZoneResponse(z.Id, z.Name, z.VehicleType.ToString(), z.Status.ToString())).ToArray())).ToArray(),
            value.Page, value.PageSize, value.TotalCount, value.TotalPages));
    }
    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(ParkingLotCreatedResponse), 201)]
    public async Task<IActionResult> Create(CreateParkingLotRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateParkingLotCommand(request.Name, request.Campus, request.OpeningTime, request.ClosingTime), cancellationToken);
        return result.IsSuccess ? Created("/api/v1/parking-lots", new ParkingLotCreatedResponse(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Update(Guid id, UpdateParkingLotRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateParkingLotCommand(id, request.Name, request.Campus, request.OpeningTime, request.ClosingTime), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ActivateParkingLotCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateParkingLotCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
}
