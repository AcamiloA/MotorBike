using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Universities;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Application.Tests.Universities;

public sealed class UniversityQueryTests
{
    [Fact]
    public async Task QueryProjectsOnlyPublicFieldsAndPreservesRepositoryOrder()
    {
        var values = new[]
        {
            new University(UniversityIds.Cmc, "CMC", "Colegio Mayor de Cundinamarca", DateTimeOffset.UtcNow),
            new University(UniversityIds.Etitc, "ETITC", "ETITC", DateTimeOffset.UtcNow)
        };
        var result = await new GetActiveUniversitiesQueryHandler(new Repository(values)).Handle(new(), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(values.Select(x => x.Id), result.Value.Select(x => x.Id));
        Assert.Equal(values.Select(x => x.Name), result.Value.Select(x => x.Name));
        Assert.Equal(values.Select(x => x.Code), result.Value.Select(x => x.Code));
    }

    [Fact]
    public async Task EmptyCatalogReturnsSuccessfulEmptyCollection()
    {
        var result = await new GetActiveUniversitiesQueryHandler(new Repository([])).Handle(new(), default);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value);
    }

    [Fact]
    public async Task QueryPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new GetActiveUniversitiesQueryHandler(new Repository([])).Handle(new(), cancellation.Token));
    }

    private sealed class Repository(IReadOnlyList<University> values) : IUniversityRepository
    {
        public Task<University?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<University>> GetActiveAsync(CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(values); }
    }
}
