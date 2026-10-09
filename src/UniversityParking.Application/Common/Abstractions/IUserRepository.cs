using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Application.Users;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Common.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken token);
    Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<UserProfile>> SearchAsync(GetUsersQuery query, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByIdentificationNumberAsync(IdentificationNumber number, CancellationToken cancellationToken);
    Task<User?> GetByCardCodeAsync(CardCode code, CancellationToken cancellationToken);
    Task<bool> ExistsByIdentificationNumberAsync(IdentificationNumber number, CancellationToken cancellationToken);
    Task<bool> ExistsByCardCodeAsync(CardCode code, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}
