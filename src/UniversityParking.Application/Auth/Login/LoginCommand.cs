using UniversityParking.Application.Common.Messaging;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Auth.Login;

public sealed record LoginCommand(string IdentificationNumber, string Password) : ICommand<LoginResult>;
public sealed record LoginUser(Guid Id, string FullName, MemberType MemberType, IReadOnlyCollection<string> Roles, InstitutionalUserType UserType = InstitutionalUserType.STUDENT);
public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAtUtc, LoginUser User,
    bool RequiresPasswordChange = false, Guid? ChallengeId = null, string? PasswordChangeToken = null);
