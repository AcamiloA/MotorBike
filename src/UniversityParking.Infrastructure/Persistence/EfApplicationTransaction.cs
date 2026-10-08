using Microsoft.EntityFrameworkCore.Storage;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Persistence;

internal sealed class EfApplicationTransaction(IDbContextTransaction transaction) : IApplicationTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
    public Task RollbackAsync(CancellationToken cancellationToken) => transaction.RollbackAsync(cancellationToken);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
