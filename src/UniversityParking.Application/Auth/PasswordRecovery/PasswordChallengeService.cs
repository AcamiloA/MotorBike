using System.Security.Cryptography;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Auth.PasswordRecovery;
public sealed class PasswordChallengeService(IPasswordChallengeRepository challenges, IClock clock)
{
    public async Task<(PasswordChallenge Challenge, string Token)> CreateAsync(Guid userId, PasswordChallengePurpose purpose, CancellationToken token)
    {
        await InvalidateAsync(userId, token);
        var secret = purpose == PasswordChallengePurpose.RECOVERY ? RandomNumberGenerator.GetInt32(10000000, 100000000).ToString() : Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var challenge = new PasswordChallenge(userId, secret, purpose, clock.UtcNow);
        await challenges.AddAsync(challenge, token); return (challenge, secret);
    }
    public async Task InvalidateAsync(Guid userId, CancellationToken token)
    {
        foreach(var challenge in await challenges.GetPendingAsync(userId, token)) challenge.Consume(clock.UtcNow);
    }
}
