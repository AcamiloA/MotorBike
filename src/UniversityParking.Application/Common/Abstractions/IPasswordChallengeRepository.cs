using UniversityParking.Domain.Users;
namespace UniversityParking.Application.Common.Abstractions;
public interface IPasswordChallengeRepository
{
    Task<PasswordChallenge?> GetAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<PasswordChallenge>> GetPendingAsync(Guid userId, CancellationToken token);
    Task AddAsync(PasswordChallenge challenge, CancellationToken token);
}
public interface IEmailSender
{
    bool IsEnabled=>true;
    Task EnsureAvailableAsync(CancellationToken token)=>Task.CompletedTask;
    Task SendAsync(string to, string subject, string body, CancellationToken token);
}
