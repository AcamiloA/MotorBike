using UniversityParking.Application.Common.Messaging;
namespace UniversityParking.Application.Auth.PasswordRecovery;
public sealed record RequestPasswordRecoveryCommand(string Email) : ICommand;
public sealed record CompletePasswordRecoveryCommand(string Email, string Code, string NewPassword) : ICommand;
public sealed record CompleteTemporaryPasswordCommand(Guid ChallengeId, string Token, string NewPassword) : ICommand;
public sealed record ResetUserPasswordCommand(Guid UserId, string TemporaryPassword) : ICommand;
