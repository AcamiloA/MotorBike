using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Domain.News;

namespace UniversityParking.Application.News;

public sealed record CreateNewsCommand(string Title, string Content) : ICommand<Guid>;
public sealed record UpdateNewsCommand(Guid NewsId, string Title, string Content) : ICommand;
public sealed record PublishNewsCommand(Guid NewsId) : ICommand;
public sealed record ArchiveNewsCommand(Guid NewsId) : ICommand;
public sealed record GetPublishedNewsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<NewsView>>;
public sealed record GetAdminNewsQuery(NewsStatus? Status = null, string? Search = null, int Page = 1, int PageSize = 20) : IQuery<PagedResult<NewsView>>;
public sealed record NewsView(Guid Id, string Title, string Content, NewsStatus Status, DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt, Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static NewsView From(NewsItem item) => new(item.Id, item.Title, item.Content, item.Status, item.PublishedAt,
        item.ArchivedAt, item.CreatedBy, item.CreatedAt, item.UpdatedAt);
}
