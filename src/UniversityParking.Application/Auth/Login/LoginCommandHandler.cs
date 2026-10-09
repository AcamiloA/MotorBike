using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Auth.Login;

public sealed class LoginCommandHandler(IUserRepository users, IUserCredentialRepository credentials,
    IRoleRepository roles, IPasswordHasher passwordHasher, ITokenService tokens,
    UniversityParking.Application.Auth.PasswordRecovery.PasswordChallengeService? challenges = null,
    IUnitOfWork? work = null) : IRequestHandler<LoginCommand, Result<LoginResult>>
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
        var statusError = user.Status switch
        {
            UserStatus.ACTIVE => null,
            UserStatus.PENDING => AuthErrors.AccountPending,
            UserStatus.REJECTED => AuthErrors.AccountRejected,
            _ => AuthErrors.UserInactive
        };
        if (statusError is not null) return Result<LoginResult>.Failure(statusError);
        var roleCodes = await roles.GetCodesByUserIdAsync(user.Id, cancellationToken);
        if (user.MustChangePassword)
        {
            if(challenges is null || work is null) throw new InvalidOperationException("Password challenges no configurados.");
            await using var transaction = await work.BeginTransactionAsync(cancellationToken);
            user = await users.GetByIdForUpdateAsync(user.Id, cancellationToken) ?? throw new InvalidOperationException();
            credential = await credentials.GetByUserIdAsync(user.Id, cancellationToken);
            if(credential is null || !passwordHasher.Verify(request.Password, credential.PasswordHash) || user.Status != UserStatus.ACTIVE)
                return Result<LoginResult>.Failure(AuthErrors.InvalidCredentials);
            var challenge = await challenges.CreateAsync(user.Id, PasswordChallengePurpose.TEMPORARY_CHANGE, cancellationToken);
            await work.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Result<LoginResult>.Success(new("",challenge.Challenge.ExpiresAt,
                new(user.Id,user.FullName,user.MemberType,[],user.UserType),true,challenge.Challenge.Id,challenge.Token));
        }
        var accessToken = tokens.CreateAccessToken(new TokenUser(user.Id, user.MemberType, roleCodes,credential.PasswordChangedAt,credential.SecurityStamp));
        return Result<LoginResult>.Success(new LoginResult(accessToken.Token, accessToken.ExpiresAtUtc,
            new LoginUser(user.Id, user.FullName, user.MemberType, roleCodes, user.UserType)));
    }
}
