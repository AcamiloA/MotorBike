using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Users;

public sealed class ActivateUserCommandHandler(UserOperationContext operation, IUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateUserCommand, Result>
{
    public async Task<Result> Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result.Failure(error);
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result.Failure(UserErrors.NotFound);
        if (user.Status == UserStatus.ACTIVE) return Result.Success();
        user.Activate(operation.UtcNow);
        await operation.AuditAsync("USER_ACTIVATED", user.Id, new { Status = "INACTIVE" }, new { Status = "ACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class DeactivateUserCommandHandler(UserOperationContext operation, IUserRepository users,
    IParkingMovementRepository movements, IUnitOfWork unitOfWork) : IRequestHandler<DeactivateUserCommand, Result>
{
    public async Task<Result> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result.Failure(error);
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result.Failure(UserErrors.NotFound);
        if (user.Status == UserStatus.INACTIVE) return Result.Success();
        if (await movements.ExistsOpenByUserIdAsync(user.Id, cancellationToken)) return Result.Failure(UserErrors.HasOpenParkingMovement);
        user.Deactivate(operation.UtcNow);
        await operation.AuditAsync("USER_DEACTIVATED", user.Id, new { Status = "ACTIVE" }, new { Status = "INACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class AssignRoleCommandHandler(UserOperationContext operation, IUserRepository users,
    IRoleRepository roles, IUnitOfWork unitOfWork) : IRequestHandler<AssignRoleCommand, Result>
{
    public async Task<Result> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result.Failure(error);
        if (await users.GetByIdAsync(request.UserId, cancellationToken) is null) return Result.Failure(UserErrors.NotFound);
        var role = await roles.GetByCodeAsync(request.RoleCode, cancellationToken);
        if (role is null) return Result.Failure(new Error("ROLE_NOT_FOUND", "El rol solicitado no existe.", ErrorType.NotFound));
        if ((await roles.GetCodesByUserIdAsync(request.UserId, cancellationToken)).Contains(role.Code)) return Result.Success();
        await roles.AssignAsync(new UserRole(request.UserId, role.Id), cancellationToken);
        await operation.AuditAsync("ROLE_ASSIGNED", request.UserId, null, new { Role = role.Code }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class RemoveRoleCommandHandler(UserOperationContext operation, IUserRepository users,
    IRoleRepository roles, IUnitOfWork unitOfWork) : IRequestHandler<RemoveRoleCommand, Result>
{
    public async Task<Result> Handle(RemoveRoleCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result.Failure(error);
        if (await users.GetByIdAsync(request.UserId, cancellationToken) is null) return Result.Failure(UserErrors.NotFound);
        var role = await roles.GetByCodeAsync(request.RoleCode, cancellationToken);
        if (role is null) return Result.Failure(new Error("ROLE_NOT_FOUND", "El rol solicitado no existe.", ErrorType.NotFound));
        if (role.Code == RoleCodes.User) return Result.Failure(UserErrors.RequiredRoleCannotBeRemoved);
        if (!(await roles.GetCodesByUserIdAsync(request.UserId, cancellationToken)).Contains(role.Code)) return Result.Success();
        await roles.RemoveAsync(request.UserId, role.Id, cancellationToken);
        await operation.AuditAsync("ROLE_REMOVED", request.UserId, new { Role = role.Code }, null, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
