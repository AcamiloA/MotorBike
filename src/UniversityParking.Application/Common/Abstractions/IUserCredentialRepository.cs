using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Common.Abstractions;

public interface IUserCredentialRepository
{
    Task AddAsync(UserCredential credential, CancellationToken cancellationToken);
    Task<UserCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
