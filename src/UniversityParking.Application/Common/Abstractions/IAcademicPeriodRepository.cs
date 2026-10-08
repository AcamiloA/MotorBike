using UniversityParking.Domain.AcademicPeriods;

namespace UniversityParking.Application.Common.Abstractions;

public interface IAcademicPeriodRepository
{
    Task<AcademicPeriod?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AcademicPeriod>> GetAllAsync(CancellationToken cancellationToken);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);
    Task<AcademicPeriod?> GetActiveForShareAsync(CancellationToken cancellationToken);
    Task<AcademicPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AcademicPeriod?> GetActiveAsync(CancellationToken cancellationToken);
    Task<bool> ExistsActiveAsync(CancellationToken cancellationToken);
    Task AddAsync(AcademicPeriod period, CancellationToken cancellationToken);
}
