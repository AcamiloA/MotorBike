using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Users;

public sealed class CreateUserCommandHandler(UserOperationContext operation, IUserRepository users,
    IUserCredentialRepository credentials, IRoleRepository roles, IPasswordHasher hasher, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result<Guid>.Failure(error);
        var identification = new IdentificationNumber(request.IdentificationNumber);
        var card = new CardCode(request.CardCode);
        if (await users.ExistsByIdentificationNumberAsync(identification, cancellationToken)) return Result<Guid>.Failure(UserErrors.AlreadyExists);
        if (await users.ExistsByCardCodeAsync(card, cancellationToken)) return Result<Guid>.Failure(UserErrors.CardCodeAlreadyExists);
        var codes = (request.Roles ?? []).Append(RoleCodes.User).Distinct(StringComparer.Ordinal).ToArray();
        var requestedRoles = new List<Role>();
        foreach (var code in codes)
        {
            var role = await roles.GetByCodeAsync(code, cancellationToken);
            if (role is null) return Result<Guid>.Failure(new Error("ROLE_NOT_FOUND", "El rol solicitado no existe.", ErrorType.NotFound));
            requestedRoles.Add(role);
        }
        var user = new User(identification, request.FullName, request.University, request.Career,
            request.MemberType, card, operation.UtcNow);
        await users.AddAsync(user, cancellationToken);
        await credentials.AddAsync(new UserCredential(user.Id, hasher.Hash(request.InitialPassword), operation.UtcNow), cancellationToken);
        foreach (var role in requestedRoles) await roles.AssignAsync(new UserRole(user.Id, role.Id), cancellationToken);
        await operation.AuditAsync("USER_CREATED", user.Id, null, new { Profile = UserOperationContext.Snapshot(user), Roles = codes }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(user.Id);
    }
}
