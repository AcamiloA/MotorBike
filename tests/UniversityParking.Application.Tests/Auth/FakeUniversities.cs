using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Application.Tests.Auth;

internal sealed class FakeUniversities : IUniversityRepository
{
    public University University { get; } = new(UniversityIds.Etitc, "ETITC", "ETITC", new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero));
    public Task<University?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == University.Id ? University : null);
    public Task<IReadOnlyList<University>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<University>>(University.IsActive ? [University] : []);
}
