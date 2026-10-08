using Microsoft.AspNetCore.Identity;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Authentication;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> hasher = new();
    private readonly object context = new();
    private readonly string dummyHash;

    public PasswordHasher() => dummyHash = hasher.HashPassword(context, Guid.NewGuid().ToString("N"));

    public string Hash(string password) => hasher.HashPassword(context, password);

    public bool Verify(string password, string passwordHash)
    {
        var missing = string.IsNullOrEmpty(passwordHash);
        try
        {
            var verified = hasher.VerifyHashedPassword(context, missing ? dummyHash : passwordHash, password);
            return !missing && verified != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
