using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Users;

public sealed class UserCredential
{
    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset PasswordChangedAt { get; private set; }
    public Guid SecurityStamp {get;private set;}

    public UserCredential(Guid userId, string passwordHash, DateTimeOffset passwordChangedAt)
    {
        UserId = Guard.Id(userId, "usuario");
        PasswordHash = Guard.Text(passwordHash, int.MaxValue, "hash de contraseña");
        PasswordChangedAt = Guard.Utc(passwordChangedAt);
        SecurityStamp=Guid.NewGuid();
    }

    public void ChangePasswordHash(string passwordHash, DateTimeOffset changedAt)
    {
        var hash = Guard.Text(passwordHash, int.MaxValue, "hash de contraseña");
        var now = Guard.Utc(changedAt);
        Guard.Chronology(now, PasswordChangedAt);
        PasswordHash = hash;
        PasswordChangedAt = now;
        SecurityStamp=Guid.NewGuid();
    }
    public void RotateSecurityStamp()=>SecurityStamp=Guid.NewGuid();
}
