namespace UniversityParking.Contracts.Auth;

public sealed record LoginRequest(string IdentificationNumber, string Password);
public sealed record LoginUserResponse(Guid Id, string FullName, string MemberType, IReadOnlyCollection<string> Roles);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, LoginUserResponse User);
