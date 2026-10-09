namespace UniversityParking.Contracts.Auth;

public sealed record LoginRequest(string IdentificationNumber, string Password);
public sealed record LoginUserResponse(Guid Id, string FullName, string MemberType, IReadOnlyCollection<string> Roles, string UserType = "STUDENT");
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, LoginUserResponse User,
    bool RequiresPasswordChange = false, Guid? ChallengeId = null, string? PasswordChangeToken = null);
public sealed record RequestPasswordRecoveryRequest(string Email);
public sealed record CompletePasswordRecoveryRequest(string Email, string Code, string NewPassword);
public sealed record CompleteTemporaryPasswordRequest(Guid ChallengeId, string Token, string NewPassword);
public sealed record ResetUserPasswordRequest(string TemporaryPassword);
