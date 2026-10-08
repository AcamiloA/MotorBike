using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.News;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Users;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.E2E.Tests.News;

[Collection("Authentication API")]
public sealed class NewsEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private HttpClient client => fixture.Client;
    private User admin = null!, user = null!, guard = null!;
    private const string Path = "/api/v1/admin/news";
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        admin = await fixture.CreateUserAsync("USER", "ADMIN");
        user = await fixture.CreateUserAsync("USER"); guard = await fixture.CreateUserAsync("USER", "GUARD");
        await Login(admin);
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private async Task Login(User actor, HttpClient? selected = null)
    {
        selected ??= client;
        var response = await selected.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(actor.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        selected.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }
    private async Task<Guid> Create(string title = "Noticia", string content = "Información universitaria")
    {
        var response = await client.PostAsJsonAsync(Path, new CreateNewsRequest(title, content));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<NewsCreatedResponse>())!.Id;
    }
    private async Task<PagedResponse<NewsResponse>> List(string path = Path) => (await client.GetFromJsonAsync<PagedResponse<NewsResponse>>(path))!;
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString()); Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }
    [Fact]
    public async Task LifecycleDraftHiddenPublishedVisibleArchivedHiddenWithAudit()
    {
        var before = DateTimeOffset.UtcNow; var id = await Create();
        var draft = Assert.Single((await List()).Items);
        Assert.Equal("DRAFT", draft.Status); Assert.Equal(admin.Id, draft.CreatedBy);
        Assert.InRange(draft.CreatedAt, before, DateTimeOffset.UtcNow); Assert.Empty((await List("/api/v1/news")).Items);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{Path}/{id}/publish", null)).StatusCode);
        await Login(user); var published = Assert.Single((await List("/api/v1/news")).Items);
        Assert.Equal(id, published.Id); Assert.Equal("PUBLISHED", published.Status); Assert.NotNull(published.PublishedAt);
        await Login(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"{Path}/{id}", new UpdateNewsRequest("Actualizada", "Contenido actualizado"))).StatusCode);
        Assert.Equal(published.PublishedAt, Assert.Single((await List()).Items).PublishedAt);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{Path}/{id}/archive", null)).StatusCode);
        Assert.Empty((await List("/api/v1/news")).Items); var archived = Assert.Single((await List()).Items);
        Assert.Equal("ARCHIVED", archived.Status); Assert.NotNull(archived.ArchivedAt);
        await Error(await client.PostAsync($"{Path}/{id}/publish", null), HttpStatusCode.Conflict, "INVALID_NEWS_STATE");
        await Error(await client.PutAsJsonAsync($"{Path}/{id}", new UpdateNewsRequest("No", "No")), HttpStatusCode.Conflict, "INVALID_NEWS_STATE");
        await using var context = fixture.CreateContext();
        Assert.Equal(new[] { "NEWS_CREATED", "NEWS_PUBLISHED", "NEWS_UPDATED", "NEWS_ARCHIVED" },
            await context.AuditLogs.Where(x => x.EntityId == id).OrderBy(x => x.CreatedAt).Select(x => x.Action).ToArrayAsync());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ArchiveDraftOrPublishedAndRepeatActionsAreIdempotent(bool published)
    {
        var id = await Create();
        if (published)
        {
            await client.PostAsync($"{Path}/{id}/publish", null);
            var first = Assert.Single((await List()).Items);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{Path}/{id}/publish", null)).StatusCode);
            Assert.Equal(first, Assert.Single((await List()).Items));
        }
        await client.PostAsync($"{Path}/{id}/archive", null);
        var archived = Assert.Single((await List()).Items);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{Path}/{id}/archive", null)).StatusCode);
        Assert.Equal(archived, Assert.Single((await List()).Items));
        await using var context = fixture.CreateContext();
        Assert.Equal(published ? 3 : 2, await context.AuditLogs.CountAsync(x => x.EntityId == id));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UserAndGuardMayReadPublishedButCannotAdminister(bool asGuard)
    {
        var id = await Create(); await client.PostAsync($"{Path}/{id}/publish", null); await Login(asGuard ? guard : user);
        Assert.Single((await List("/api/v1/news")).Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Path, new CreateNewsRequest("No", "No"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Path}/{id}", new UpdateNewsRequest("No", "No"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Path}/{id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Path}/{id}/archive", null)).StatusCode);
    }
    [Fact]
    public async Task AnonymousCannotReadEitherList()
    {
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/news")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path)).StatusCode);
    }
    [Theory]
    [InlineData("", "Contenido")] [InlineData("Título", "  ")]
    public async Task RequiredTextIsValidated(string title, string content) =>
        await Error(await client.PostAsJsonAsync(Path, new CreateNewsRequest(title, content)), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    [Fact]
    public async Task RejectsLongTitleAndInjectedCreatorStateDates()
    {
        await Error(await client.PostAsJsonAsync(Path, new CreateNewsRequest(new string('x', 201), "Contenido")), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        foreach (var name in new[] { "createdBy", "status", "publishedAt" })
            await Error(await client.PostAsJsonAsync(Path, new Dictionary<string, object> { ["title"] = "Título", ["content"] = "Contenido", [name] = "injected" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Theory]
    [InlineData("publish")] [InlineData("archive")]
    public async Task MissingNewsReturnsNotFound(string action) =>
        await Error(await client.PostAsync($"{Path}/{Guid.NewGuid()}/{action}", null), HttpStatusCode.NotFound, "NEWS_NOT_FOUND");
    [Theory]
    [InlineData("page=0")] [InlineData("pageSize=101")] [InlineData("pageSize=0")]
    public async Task BothListsValidatePagination(string query)
    {
        await Error(await client.GetAsync(Path + "?" + query), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.GetAsync("/api/v1/news?" + query), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Fact]
    public async Task AdministrativeSearchIsCaseInsensitiveLiteralAndFiltersStates()
    {
        var draft = await Create("Cierre 100%", "Aviso especial");
        var published = await Create("Apertura", "Aviso público"); await client.PostAsync($"{Path}/{published}/publish", null);
        var archived = await Create("Archivo", "Histórico"); await client.PostAsync($"{Path}/{archived}/archive", null);
        Assert.Equal(draft, Assert.Single((await List(Path + "?status=DRAFT&search=CIERRE")).Items).Id);
        Assert.Equal(published, Assert.Single((await List(Path + "?status=PUBLISHED&search=P%C3%9ABLICO")).Items).Id);
        Assert.Equal(archived, Assert.Single((await List(Path + "?status=ARCHIVED")).Items).Id);
        Assert.Equal(draft, Assert.Single((await List(Path + "?search=%25")).Items).Id);
        Assert.Empty((await List(Path + "?search=_")).Items);
        await Error(await client.GetAsync(Path + "?status=999"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.GetAsync(Path + "?search=" + new string('x', 201)), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Fact]
    public async Task PublishedListSortsByPublicationTimeAndHasStablePagination()
    {
        var first = await Create("Primera"); var second = await Create("Segunda");
        await client.PostAsync($"{Path}/{second}/publish", null); await client.PostAsync($"{Path}/{first}/publish", null);
        var page = await List("/api/v1/news?pageSize=1"); Assert.Equal(2, page.TotalCount); Assert.Equal(2, page.TotalPages);
        Assert.Equal(first, Assert.Single(page.Items).Id);
        Assert.Equal(second, Assert.Single((await List("/api/v1/news?pageSize=1&page=2")).Items).Id);
        Assert.Empty((await List("/api/v1/news?page=2147483647")).Items);
    }
    [Fact]
    public async Task StaleAdminClaimsCannotWriteOrReadAdminList()
    {
        await using var context = fixture.CreateContext(); var role = await context.Roles.SingleAsync(x => x.Code == "ADMIN");
        context.UserRoles.Remove(await context.UserRoles.SingleAsync(x => x.UserId == admin.Id && x.RoleId == role.Id)); await context.SaveChangesAsync();
        await Error(await client.GetAsync(Path), HttpStatusCode.Forbidden, "FORBIDDEN");
        await Error(await client.PostAsJsonAsync(Path, new CreateNewsRequest("No", "No")), HttpStatusCode.Forbidden, "FORBIDDEN");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/news")).StatusCode);
    }
    [Fact]
    public async Task FailedAuditRollsBackCreationAndStateTransition()
    {
        var id = await Create();
        await using var factory = fixture.CreateFactory(services => services.AddScoped<IAuditLogRepository, FailingAudit>());
        using var selected = factory.CreateClient(new() { BaseAddress = new("https://localhost") }); await Login(admin, selected);
        Assert.Equal(HttpStatusCode.InternalServerError, (await selected.PostAsJsonAsync(Path, new CreateNewsRequest("Falla", "Falla"))).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await selected.PostAsync($"{Path}/{id}/publish", null)).StatusCode);
        await using var context = fixture.CreateContext(); var item = await context.NewsItems.SingleAsync();
        Assert.Equal(id, item.Id); Assert.Equal(NewsStatus.DRAFT, item.Status); Assert.Null(item.PublishedAt); Assert.Single(await context.AuditLogs.ToListAsync());
    }
    [Fact]
    public async Task ConcurrentPublicationByDifferentAdminsCreatesOneAuditAndOneTimestamp()
    {
        var id = await Create(); var other = await fixture.CreateUserAsync("USER", "ADMIN");
        using var selected = fixture.Factory.CreateClient(new() { BaseAddress = new("https://localhost") }); await Login(other, selected);
        var responses = await Task.WhenAll(client.PostAsync($"{Path}/{id}/publish", null), selected.PostAsync($"{Path}/{id}/publish", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        await using var context = fixture.CreateContext(); Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "NEWS_PUBLISHED"));
        Assert.NotNull((await context.NewsItems.SingleAsync()).PublishedAt);
    }
    private sealed class FailingAudit(AppDbContext context) : IAuditLogRepository
    {
        public async Task AddAsync(AuditLog audit, CancellationToken token) =>
            await context.AuditLogs.AddAsync(new(Guid.NewGuid(), audit.Action, audit.EntityType, audit.EntityId, audit.CreatedAt), token);
    }
}
