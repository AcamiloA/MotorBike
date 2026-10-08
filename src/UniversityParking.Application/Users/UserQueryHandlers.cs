using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Users;

public sealed class GetMyProfileQueryHandler(UserOperationContext operation, IUserRepository users, IRoleRepository roles)
    : IRequestHandler<GetMyProfileQuery, Result<UserProfile>>
{
    public async Task<Result<UserProfile>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(false, cancellationToken) is { } error) return Result<UserProfile>.Failure(error);
        var user = (await users.GetByIdAsync(operation.ActorId!.Value, cancellationToken))!;
        return Result<UserProfile>.Success(UserProfile.From(user, await roles.GetCodesByUserIdAsync(user.Id, cancellationToken)));
    }
}
public sealed class GetUserByIdQueryHandler(UserOperationContext operation, IUserRepository users, IRoleRepository roles)
    : IRequestHandler<GetUserByIdQuery, Result<UserProfile>>
{
    public async Task<Result<UserProfile>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result<UserProfile>.Failure(error);
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        return user is null ? Result<UserProfile>.Failure(UserErrors.NotFound) :
            Result<UserProfile>.Success(UserProfile.From(user, await roles.GetCodesByUserIdAsync(user.Id, cancellationToken)));
    }
}
public sealed class GetUsersQueryHandler(UserOperationContext operation, IUserRepository users)
    : IRequestHandler<GetUsersQuery, Result<PagedResult<UserProfile>>>
{
    public async Task<Result<PagedResult<UserProfile>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result<PagedResult<UserProfile>>.Failure(error);
        return Result<PagedResult<UserProfile>>.Success(await users.SearchAsync(request, cancellationToken));
    }
}
