using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Users;

// Writes the account components; authorization, state, audit and transaction remain in each use case.
public sealed class AccountProvisioner(IUserRepository users, IUserCredentialRepository credentials,
    IRoleRepository roles, IPasswordHasher hasher)
{
    public async Task AddAsync(User user, string password, IReadOnlyCollection<Role> assignments,
        CancellationToken token)
    {
        await users.AddAsync(user, token);
        await credentials.AddAsync(new UserCredential(user.Id, hasher.Hash(password), user.CreatedAt), token);
        foreach (var role in assignments) await roles.AssignAsync(new UserRole(user.Id, role.Id), token);
    }
}
