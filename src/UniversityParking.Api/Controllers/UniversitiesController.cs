using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Api.RateLimiting;
using UniversityParking.Application.Universities;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Universities;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/universities")]
public sealed class UniversitiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicCatalog)]
    [ProducesResponseType(typeof(UniversityResponse[]), 200)]
    [ProducesResponseType(typeof(ApiProblemDetails), 429)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetActiveUniversitiesQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(x => new UniversityResponse(x.Id, x.Code, x.Name)).ToArray())
            : ProblemResponses.Map(HttpContext, result.Error!);
    }
}
