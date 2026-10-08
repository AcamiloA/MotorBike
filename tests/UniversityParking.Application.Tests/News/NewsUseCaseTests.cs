using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.News;
using UniversityParking.Application.Tests.Auth;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Tests.News;

public sealed class NewsUseCaseTests
{
    private readonly FakeClock clock = new();
    private readonly FakeCurrentUser actor = new() { Roles = ["ADMIN"] };
    private readonly FakeUsers users = new();
    private readonly FakeRoles roles = new() { Codes = ["ADMIN"] };
    private readonly FakeUnitOfWork unit = new();
    private readonly Store store = new();
    private readonly NewsCommandHandlers commands;
    private readonly NewsQueryHandlers queries;
    public NewsUseCaseTests()
    {
        var user = new User(new IdentificationNumber("123"), "Admin", "ETITC", null, MemberType.STAFF, new CardCode("CARD"), clock.UtcNow);
        users.Values[user.Id] = user; actor.UserId = user.Id;
        var operation = new AdministrationOperationContext(actor, users, roles, store, clock, new Request());
        commands = new(operation, actor, store, unit); queries = new(operation, store);
    }
    [Fact]
    public async Task CreateUsesSessionClockDraftAndAudit()
    {
        var result = await commands.Handle(new CreateNewsCommand("Título", "Contenido"), default);
        Assert.True(result.IsSuccess); Assert.Equal(actor.UserId, store.Item!.CreatedBy);
        Assert.Equal(clock.UtcNow, store.Item.CreatedAt); Assert.Equal(NewsStatus.DRAFT, store.Item.Status);
        Assert.Equal("NEWS_CREATED", Assert.Single(store.Audits).Action); Assert.Equal(1, unit.SaveCount);
    }
    [Theory]
    [InlineData("USER")] [InlineData("GUARD")]
    public async Task NonAdminCannotWriteOrReadAdministrativeList(string role)
    {
        actor.Roles = roles.Codes = [role];
        Assert.Equal("FORBIDDEN", (await commands.Handle(new CreateNewsCommand("Título", "Contenido"), default)).Error!.Code);
        Assert.Equal("FORBIDDEN", (await commands.Handle(new UpdateNewsCommand(Guid.NewGuid(), "Título", "Contenido"), default)).Error!.Code);
        Assert.Equal("FORBIDDEN", (await commands.Handle(new PublishNewsCommand(Guid.NewGuid()), default)).Error!.Code);
        Assert.Equal("FORBIDDEN", (await commands.Handle(new ArchiveNewsCommand(Guid.NewGuid()), default)).Error!.Code);
        Assert.Equal("FORBIDDEN", (await queries.Handle(new GetAdminNewsQuery(), default)).Error!.Code);
        Assert.Equal(0, unit.SaveCount);
    }
    [Fact]
    public async Task PublishedQueryForcesPublishedStateForNormalUser()
    {
        actor.Roles = roles.Codes = ["USER"];
        Assert.True((await queries.Handle(new GetPublishedNewsQuery(2, 10), default)).IsSuccess);
        Assert.Equal(NewsStatus.PUBLISHED, store.Filter); Assert.True(store.PublishedOrder); Assert.Equal(2, store.Page);
    }
    [Fact]
    public async Task RevokedAdminRoleCannotCreate()
    {
        roles.Codes = ["USER"];
        Assert.Equal("FORBIDDEN", (await commands.Handle(new CreateNewsCommand("Título", "Contenido"), default)).Error!.Code);
        Assert.Empty(store.Audits);
    }
    [Fact]
    public async Task InactiveAccountCannotReadPublishedNews()
    {
        users.Values[actor.UserId!.Value].Deactivate(clock.UtcNow);
        Assert.Equal("USER_INACTIVE", (await queries.Handle(new GetPublishedNewsQuery(), default)).Error!.Code);
    }
    [Fact]
    public async Task PublishIsIdempotentAndUpdatePreservesPublicationDate()
    {
        var id = (await commands.Handle(new CreateNewsCommand("Título", "Contenido"), default)).Value;
        await commands.Handle(new PublishNewsCommand(id), default);
        var published = store.Item!.PublishedAt;
        clock.UtcNow = clock.UtcNow.AddDays(1);
        await commands.Handle(new PublishNewsCommand(id), default);
        Assert.Equal(2, unit.SaveCount); Assert.Equal(2, store.Audits.Count);
        await commands.Handle(new UpdateNewsCommand(id, "Nuevo", "Modificado"), default);
        Assert.Equal(published, store.Item.PublishedAt); Assert.Equal(NewsStatus.PUBLISHED, store.Item.Status);
        Assert.Equal(clock.UtcNow, store.Item.UpdatedAt); Assert.Equal("NEWS_UPDATED", store.Audits.Last().Action);
        Assert.Equal(3, store.LockCount);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ArchiveBothStatesIsIdempotentAndRejectsSubsequentEditPublish(bool published)
    {
        var id = (await commands.Handle(new CreateNewsCommand("Título", "Contenido"), default)).Value;
        if (published) await commands.Handle(new PublishNewsCommand(id), default);
        await commands.Handle(new ArchiveNewsCommand(id), default);
        var snapshot = NewsView.From(store.Item!); var saves = unit.SaveCount;
        clock.UtcNow = clock.UtcNow.AddDays(1);
        Assert.True((await commands.Handle(new ArchiveNewsCommand(id), default)).IsSuccess);
        Assert.Equal(snapshot, NewsView.From(store.Item!)); Assert.Equal(saves, unit.SaveCount);
        Assert.Equal("INVALID_NEWS_STATE", (await commands.Handle(new PublishNewsCommand(id), default)).Error!.Code);
        Assert.Equal("INVALID_NEWS_STATE", (await commands.Handle(new UpdateNewsCommand(id, "Otro", "Otro"), default)).Error!.Code);
    }
    [Fact]
    public async Task MissingNewsReturnsNotFound()
    {
        Assert.Equal("NEWS_NOT_FOUND", (await commands.Handle(new PublishNewsCommand(Guid.NewGuid()), default)).Error!.Code);
        Assert.Equal(0, unit.SaveCount);
    }
    private sealed class Request : IRequestContext { public string? IpAddress => "127.0.0.1"; public string? TraceId => "news-test"; }
    private sealed class Store : INewsRepository, IAuditLogRepository
    {
        public NewsItem? Item; public int LockCount, Page; public NewsStatus? Filter; public bool PublishedOrder;
        public List<AuditLog> Audits { get; } = [];
        public Task AddAsync(AuditLog audit, CancellationToken token) { Audits.Add(audit); return Task.CompletedTask; }
        public Task AddAsync(NewsItem item, CancellationToken token) { Item = item; return Task.CompletedTask; }
        public Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken token) => Task.FromResult(Item?.Id == id ? Item : null);
        public Task<NewsItem?> GetByIdForUpdateAsync(Guid id, CancellationToken token) { LockCount++; return GetByIdAsync(id, token); }
        public Task<PagedResult<NewsView>> SearchAsync(NewsStatus? status, string? search, bool publishedOrder, int page, int size, CancellationToken token)
        { Filter = status; PublishedOrder = publishedOrder; Page = page; return Task.FromResult(new PagedResult<NewsView>([], page, size, 0)); }
    }
}
