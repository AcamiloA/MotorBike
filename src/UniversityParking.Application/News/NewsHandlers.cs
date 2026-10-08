using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.News;

public sealed class NewsCommandHandlers(AdministrationOperationContext operation, ICurrentUser actor,
    INewsRepository news, IUnitOfWork unitOfWork) :
    IRequestHandler<CreateNewsCommand, Result<Guid>>, IRequestHandler<UpdateNewsCommand, Result>,
    IRequestHandler<PublishNewsCommand, Result>, IRequestHandler<ArchiveNewsCommand, Result>
{
    public async Task<Result<Guid>> Handle(CreateNewsCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } permission) return Result<Guid>.Failure(permission);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } lockedPermission) return Result<Guid>.Failure(lockedPermission);
        var item = new NewsItem(request.Title, request.Content, actor.UserId!.Value, operation.UtcNow);
        await news.AddAsync(item, cancellationToken);
        await operation.AuditAsync("NEWS_CREATED", "News", item.Id, null, NewsView.From(item), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<Guid>.Success(item.Id);
    }
    public Task<Result> Handle(UpdateNewsCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.NewsId, "NEWS_UPDATED", x => x.Update(request.Title, request.Content, operation.UtcNow), null, cancellationToken);
    public Task<Result> Handle(PublishNewsCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.NewsId, "NEWS_PUBLISHED", x => x.Publish(operation.UtcNow), NewsStatus.PUBLISHED, cancellationToken);
    public Task<Result> Handle(ArchiveNewsCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.NewsId, "NEWS_ARCHIVED", x => x.Archive(operation.UtcNow), NewsStatus.ARCHIVED, cancellationToken);
    private async Task<Result> ChangeAsync(Guid id, string action, Action<NewsItem> change, NewsStatus? idempotentStatus, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } permission) return Result.Failure(permission);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } lockedPermission) return Result.Failure(lockedPermission);
        var item = await news.GetByIdForUpdateAsync(id, cancellationToken);
        if (item is null) return Result.Failure(NewsErrors.NotFound);
        if (item.Status == idempotentStatus) return Result.Success();
        if (item.Status == NewsStatus.ARCHIVED) return Result.Failure(NewsErrors.InvalidState);
        var before = NewsView.From(item);
        change(item);
        await operation.AuditAsync(action, "News", item.Id, before, NewsView.From(item), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class NewsQueryHandlers(AdministrationOperationContext operation, INewsRepository news) :
    IRequestHandler<GetPublishedNewsQuery, Result<PagedResult<NewsView>>>, IRequestHandler<GetAdminNewsQuery, Result<PagedResult<NewsView>>>
{
    public async Task<Result<PagedResult<NewsView>>> Handle(GetPublishedNewsQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([], cancellationToken) is { } permission) return Result<PagedResult<NewsView>>.Failure(permission);
        return Result<PagedResult<NewsView>>.Success(await news.SearchAsync(NewsStatus.PUBLISHED, null, true, request.Page, request.PageSize, cancellationToken));
    }
    public async Task<Result<PagedResult<NewsView>>> Handle(GetAdminNewsQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } permission) return Result<PagedResult<NewsView>>.Failure(permission);
        return Result<PagedResult<NewsView>>.Success(await news.SearchAsync(request.Status, request.Search?.Trim(), false, request.Page, request.PageSize, cancellationToken));
    }
}
