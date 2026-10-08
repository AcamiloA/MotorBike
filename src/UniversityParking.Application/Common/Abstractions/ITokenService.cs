using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Common.Abstractions;

public sealed record TokenUser(Guid Id, MemberType MemberType, IReadOnlyCollection<string> Roles);
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(TokenUser user);
}
