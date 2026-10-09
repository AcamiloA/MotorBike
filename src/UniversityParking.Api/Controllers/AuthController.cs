using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UniversityParking.Application.Auth.ChangePassword;
using UniversityParking.Application.Auth.Login;
using UniversityParking.Application.Auth.Registration;
using UniversityParking.Application.Common.Results;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("password-recovery/request"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> RequestRecovery(RequestPasswordRecoveryRequest request,CancellationToken token)
    {
        var result=await sender.Send(new UniversityParking.Application.Auth.PasswordRecovery.RequestPasswordRecoveryCommand(request.Email),token);
        return result.IsSuccess?Ok(new {Message="Si existe una cuenta asociada a este correo, recibirás instrucciones para restablecer tu contraseña."}):ProblemResponses.Map(HttpContext,result.Error!);
    }
    [HttpPost("password-recovery/complete"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> CompleteRecovery(CompletePasswordRecoveryRequest request,CancellationToken token)
    {
        var result=await sender.Send(new UniversityParking.Application.Auth.PasswordRecovery.CompletePasswordRecoveryCommand(request.Email,request.Code,request.NewPassword),token);
        return result.IsSuccess?NoContent():ProblemResponses.Map(HttpContext,result.Error!);
    }
    [HttpPost("temporary-password/complete"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> CompleteTemporary(CompleteTemporaryPasswordRequest request,CancellationToken token)
    {
        var result=await sender.Send(new UniversityParking.Application.Auth.PasswordRecovery.CompleteTemporaryPasswordCommand(request.ChallengeId,request.Token,request.NewPassword),token);
        return result.IsSuccess?NoContent():ProblemResponses.Map(HttpContext,result.Error!);
    }
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
            new LoginUserResponse(value.User.Id, value.User.FullName, value.User.MemberType.ToString(), value.User.Roles,value.User.UserType.ToString()),
            value.RequiresPasswordChange,value.ChallengeId,value.PasswordChangeToken));
    }

    [HttpPost("register/student")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.StudentRegistration)]
    [ProducesResponseType(typeof(RegisterStudentResponse), 201)]
    [ProducesResponseType(typeof(ApiProblemDetails), 400)]
    [ProducesResponseType(typeof(ApiProblemDetails), 404)]
    [ProducesResponseType(typeof(ApiProblemDetails), 409)]
    [ProducesResponseType(typeof(ApiProblemDetails), 429)]
    public async Task<IActionResult> RegisterStudent(RegisterStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterStudentCommand(request.IdentificationNumber, request.FullName,
            request.UniversityId, request.Career, request.CardCode, request.Password,request.Email,request.PhoneNumber), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        return StatusCode(StatusCodes.Status201Created, new RegisterStudentResponse(result.Value.UserId, result.Value.Status.ToString()));
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
