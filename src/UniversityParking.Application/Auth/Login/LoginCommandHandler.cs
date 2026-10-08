using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Auth.Login;

public sealed class LoginCommandHandler(IUserRepository users, IUserCredentialRepository credentials,
    IRoleRepository roles, IPasswordHasher passwordHasher, ITokenService tokens) : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdentificationNumberAsync(new IdentificationNumber(request.IdentificationNumber), cancellationToken);
        if (user is null)
        {
            passwordHasher.Verify(request.Password, string.Empty);
            return Result<LoginResult>.Failure(AuthErrors.InvalidCredentials);
        }
        var credential = await credentials.GetByUserIdAsync(user.Id, cancellationToken);
        var verified = passwordHasher.Verify(request.Password, credential?.PasswordHash ?? string.Empty);
        if (!verified || credential is null) return Result<LoginResult>.Failure(AuthErrors.InvalidCredentials);
        if (user.Status != UserStatus.ACTIVE) return Result<LoginResult>.Failure(AuthErrors.UserInactive);
        var roleCodes = await roles.GetCodesByUserIdAsync(user.Id, cancellationToken);
        var accessToken = tokens.CreateAccessToken(new TokenUser(user.Id, user.MemberType, roleCodes));
        return Result<LoginResult>.Success(new LoginResult(accessToken.Token, accessToken.ExpiresAtUtc,
            new LoginUser(user.Id, user.FullName, user.MemberType, roleCodes)));
    }
}
