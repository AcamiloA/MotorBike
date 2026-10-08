using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Common.Abstractions;

public interface IRoleRepository
{
    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task AssignAsync(UserRole assignment, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid roleId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetCodesByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
