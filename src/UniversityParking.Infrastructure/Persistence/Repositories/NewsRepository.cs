using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.News;
using UniversityParking.Application.News;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class NewsRepository(AppDbContext context) : INewsRepository
{
    public Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.NewsItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(NewsItem news, CancellationToken cancellationToken) => await context.NewsItems.AddAsync(news, cancellationToken);
    public async Task<NewsItem?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await context.NewsItems.FromSqlInterpolated($"SELECT * FROM news WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (item is not null) await context.Entry(item).ReloadAsync(cancellationToken);
        return item;
    }
    public async Task<PagedResult<NewsView>> SearchAsync(NewsStatus? status, string? search, bool publishedOrder, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var query = context.NewsItems.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x => EF.Functions.ILike(x.Title, pattern, "\\") || EF.Functions.ILike(x.Content, pattern, "\\"));
        }
        var count = await query.LongCountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        var ordered = publishedOrder ? query.OrderByDescending(x => x.PublishedAt).ThenBy(x => x.Id) : query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id);
        var items = offset >= count ? [] : await ordered.Skip(checked((int)offset)).Take(pageSize).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(items.Select(NewsView.From), page, pageSize, count);
    }
}
