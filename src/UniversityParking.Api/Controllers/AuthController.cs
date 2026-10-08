using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UniversityParking.Application.Auth.ChangePassword;
using UniversityParking.Application.Auth.Login;
using UniversityParking.Application.Common.Results;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(ApiProblemDetails), 400)]
    [ProducesResponseType(typeof(ApiProblemDetails), 401)]
    [ProducesResponseType(typeof(ApiProblemDetails), 429)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LoginCommand(request.IdentificationNumber, request.Password), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var value = result.Value;
        return Ok(new LoginResponse(value.AccessToken, value.ExpiresAtUtc,
            new LoginUserResponse(value.User.Id, value.User.FullName, value.User.MemberType.ToString(), value.User.Roles)));
    }

    [HttpPost("change-password")]
    [Authorize(Policy = PolicyNames.Authenticated)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiProblemDetails), 400)]
    [ProducesResponseType(typeof(ApiProblemDetails), 401)]
    [ProducesResponseType(typeof(ApiProblemDetails), 409)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
}
