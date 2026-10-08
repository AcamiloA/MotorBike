using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Users;

public sealed class UpdateMyProfileCommandHandler(UserOperationContext operation, IUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateMyProfileCommand, Result>
{
    public async Task<Result> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(false, cancellationToken) is { } error) return Result.Failure(error);
        var user = (await users.GetByIdAsync(operation.ActorId!.Value, cancellationToken))!;
        if (user.MemberType == MemberType.STUDENT && string.IsNullOrWhiteSpace(request.Career))
            return Result.Failure(CommonErrors.Validation(new Dictionary<string, IReadOnlyList<string>>
                { [nameof(request.Career)] = ["La carrera es obligatoria para estudiantes."] }));
        var before = UserOperationContext.Snapshot(user);
        user.UpdateProfile(request.FullName, request.Career, operation.UtcNow);
        await operation.AuditAsync("USER_UPDATED", user.Id, before, UserOperationContext.Snapshot(user), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class UpdateUserCommandHandler(UserOperationContext operation, IUserRepository users,
    IVehicleRepository vehicles, IUnitOfWork unitOfWork) : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result.Failure(error);
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result.Failure(UserErrors.NotFound);
        var card = new CardCode(request.CardCode);
        var cardOwner = await users.GetByCardCodeAsync(card, cancellationToken);
        if (cardOwner is not null && cardOwner.Id != user.Id) return Result.Failure(UserErrors.CardCodeAlreadyExists);
        if (request.MemberType == MemberType.STUDENT && user.MemberType != MemberType.STUDENT &&
            await vehicles.HasActiveCarOwnedByUserAsync(user.Id, cancellationToken)) return Result.Failure(UserErrors.InvalidMemberTypeChange);
        var before = UserOperationContext.Snapshot(user);
        user.Update(request.FullName, request.University, request.Career, request.MemberType, card, operation.UtcNow);
        await operation.AuditAsync("USER_UPDATED", user.Id, before, UserOperationContext.Snapshot(user), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
