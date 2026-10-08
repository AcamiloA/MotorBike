using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Auth.ChangePassword;

public sealed class ChangePasswordCommandHandler(ICurrentUser currentUser, IUserRepository users,
    IUserCredentialRepository credentials, IPasswordHasher passwordHasher, IClock clock, IUnitOfWork unitOfWork)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Result.Failure(AuthErrors.InvalidCredentials);
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return Result.Failure(UserErrors.NotFound);
        if (user.Status != UserStatus.ACTIVE) return Result.Failure(UserErrors.Inactive);
        var credential = await credentials.GetByUserIdAsync(userId, cancellationToken);
        if (credential is null || !passwordHasher.Verify(request.CurrentPassword, credential.PasswordHash))
            return Result.Failure(AuthErrors.InvalidCurrentPassword);
        if (passwordHasher.Verify(request.NewPassword, credential.PasswordHash))
            return Result.Failure(CommonErrors.Validation(new Dictionary<string, IReadOnlyList<string>>
                { [nameof(request.NewPassword)] = new[] { "La nueva contraseña debe ser diferente de la actual." } }));
        credential.ChangePasswordHash(passwordHasher.Hash(request.NewPassword), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
