using System.Security.Cryptography;
using System.Text;
using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Users;

public enum PasswordChallengePurpose { RECOVERY, TEMPORARY_CHANGE }
public sealed class PasswordChallenge : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public PasswordChallengePurpose Purpose { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public int FailedAttempts { get; private set; }
    private PasswordChallenge() { }
    public PasswordChallenge(Guid userId, string token, PasswordChallengePurpose purpose, DateTimeOffset now)
    {
        UserId = Guard.Id(userId, "usuario"); Purpose = Guard.Defined(purpose);
        CreatedAt = Guard.Utc(now); ExpiresAt = CreatedAt.AddMinutes(purpose == PasswordChallengePurpose.RECOVERY ? 20 : 5);
        TokenHash = Hash(token);
    }
    public bool Verify(string token, DateTimeOffset now)
    {
        if (ConsumedAt != null || now >= ExpiresAt || FailedAttempts >= 5) return false;
        if (CryptographicOperations.FixedTimeEquals(Convert.FromHexString(TokenHash), Convert.FromHexString(Hash(token)))) return true;
        FailedAttempts++; if (FailedAttempts >= 5) ConsumedAt = now; return false;
    }
    public void Consume(DateTimeOffset now) => ConsumedAt = Guard.Utc(now);
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
