using UniversityParking.Domain.Universities;

namespace UniversityParking.Application.Common.Abstractions;

public interface IUniversityRepository
{
    Task<University?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<University>> GetActiveAsync(CancellationToken cancellationToken);
}
