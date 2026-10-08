using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Application.AcademicPeriods;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.Common;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/academic-periods")]
[Authorize(Policy = PolicyNames.Authenticated)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
[ProducesResponseType(typeof(ApiProblemDetails), 409)]
public sealed class AcademicPeriodsController(ISender sender) : ControllerBase
{
    [HttpGet("current")]
    [ProducesResponseType(typeof(AcademicPeriodResponse), 200)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentAcademicPeriodQuery(), cancellationToken);
        return result.IsSuccess ? Ok(Map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(AcademicPeriodResponse[]), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAcademicPeriodsQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value.Select(Map).ToArray()) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(AcademicPeriodCreatedResponse), 201)]
    public async Task<IActionResult> Create(CreateAcademicPeriodRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateAcademicPeriodCommand(request.Name, request.StartsOn, request.EndsOn), cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/academic-periods", new AcademicPeriodCreatedResponse(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ActivateAcademicPeriodCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CloseAcademicPeriodCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    private static AcademicPeriodResponse Map(AcademicPeriodView value) => new(value.Id, value.Name, value.StartsOn, value.EndsOn, value.Status.ToString());
}
