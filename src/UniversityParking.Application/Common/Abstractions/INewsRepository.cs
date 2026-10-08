using UniversityParking.Domain.News;
using UniversityParking.Application.News;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Common.Abstractions;

public interface INewsRepository
{
    Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(NewsItem news, CancellationToken cancellationToken);
    Task<NewsItem?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<NewsView>> SearchAsync(NewsStatus? status, string? search, bool publishedOrder, int page, int pageSize, CancellationToken cancellationToken);
}
