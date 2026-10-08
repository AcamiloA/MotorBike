using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class UniversityRepository(AppDbContext context) : IUniversityRepository
{
    public Task<University?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Universities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<University>> GetActiveAsync(CancellationToken cancellationToken) =>
        await context.Universities.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).ThenBy(x => x.Code).ToListAsync(cancellationToken);
}
