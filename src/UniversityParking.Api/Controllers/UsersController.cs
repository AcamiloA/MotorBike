using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Application.Users;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = PolicyNames.Authenticated)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
[ProducesResponseType(typeof(ApiProblemDetails), 409)]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), 200)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyProfileQuery(), cancellationToken);
        return result.IsSuccess ? Ok(Map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPut("me")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> UpdateMyProfile(UpdateMyProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateMyProfileCommand(request.FullName, request.Career), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(PagedResponse<UserListItemResponse>), 200)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken, [FromQuery] string? search = null,
        [FromQuery] UserMemberType? memberType = null, [FromQuery] UserAccountStatus? status = null,
        [FromQuery] string? role = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetUsersQuery(search, memberType.HasValue ? (MemberType)memberType.Value : null,
            status.HasValue ? (UserStatus)status.Value : null, role, page, pageSize), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var value = result.Value;
        var items = value.Items.Select(x => new UserListItemResponse(x.Id, x.IdentificationNumber, x.FullName,
            x.University, x.Career, x.MemberType.ToString(), x.CardCode, x.Status.ToString(), x.Roles)).ToArray();
        return Ok(new PagedResponse<UserListItemResponse>(items, value.Page, value.PageSize, value.TotalCount, value.TotalPages));
    }
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(UserProfileResponse), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(Map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(UserCreatedResponse), 201)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateUserCommand(request.IdentificationNumber, request.FullName,
            request.University, request.Career, (MemberType)request.MemberType, request.CardCode, request.InitialPassword, request.Roles), cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new UserCreatedResponse(result.Value)) :
            ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateUserCommand(id, request.FullName, request.University, request.Career,
            (MemberType)request.MemberType, request.CardCode), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ActivateUserCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateUserCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("{id:guid}/roles")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> AssignRole(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AssignRoleCommand(id, request.Role), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpDelete("{id:guid}/roles/{role}")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveRole(Guid id, string role, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveRoleCommand(id, role), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    private static UserProfileResponse Map(UserProfile value) => new(value.Id, value.IdentificationNumber,
        value.FullName, value.University, value.Career, value.MemberType.ToString(), value.CardCode, value.Status.ToString(), value.Roles);
}
