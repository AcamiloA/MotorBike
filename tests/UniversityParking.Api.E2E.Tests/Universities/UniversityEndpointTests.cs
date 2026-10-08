using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Api.RateLimiting;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Universities;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Api.E2E.Tests.Universities;

[Collection("Authentication API")]
public sealed class UniversityEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnonymousGetsExactMinimalCatalogInStableOrder()
    {
        var response = await fixture.Client.GetAsync("/api/v1/universities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var universities = (await response.Content.ReadFromJsonAsync<UniversityResponse[]>())!;
        Assert.Equal(new[] { UniversityIds.Cmc, UniversityIds.Etitc, UniversityIds.Upn }, universities.Select(x => x.Id));
        Assert.Equal(new[] { "CMC", "ETITC", "UPN" }, universities.Select(x => x.Code));
        Assert.Equal(new[] { "Colegio Mayor de Cundinamarca", "ETITC", "U. Pedagógica" }, universities.Select(x => x.Name));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.All(json.RootElement.EnumerateArray(), value =>
            Assert.Equal(new[] { "code", "id", "name" }, value.EnumerateObject().Select(x => x.Name).Order()));
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.Users.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveReferencesAreExcludedAndEmptyCatalogIs200(bool allInactive)
    {
        await using var context = fixture.CreateContext();
        var values = await context.Universities.ToListAsync();
        var original = values.ToDictionary(x => x.Id, x => x.UpdatedAt);
        foreach (var value in values.Where(x => allInactive || x.Id == UniversityIds.Cmc)) value.Deactivate(value.UpdatedAt.AddDays(1));
        await context.SaveChangesAsync();
        try
        {
            var response = await fixture.Client.GetAsync("/api/v1/universities");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var catalog = (await response.Content.ReadFromJsonAsync<UniversityResponse[]>())!;
            if (allInactive) Assert.Empty(catalog);
            else Assert.Equal(new[] { "ETITC", "UPN" }, catalog.Select(x => x.Code));
        }
        finally
        {
            context.ChangeTracker.Clear();
            foreach (var pair in original)
                await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE universities SET is_active = true, updated_at = {pair.Value} WHERE id = {pair.Key}");
        }
    }

    [Theory]
    [InlineData("USER")]
    [InlineData("GUARD")]
    [InlineData("ADMIN")]
    public async Task CatalogIsAlsoAvailableToAuthenticatedRoles(string role)
    {
        var user = await fixture.CreateUserAsync(role);
        var login = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/universities")).StatusCode);
    }

    [Fact]
    public async Task PublicCatalogRateLimitReturnsStandardProblemWithoutLimitingHealth()
    {
        for (var i = 0; i < RateLimitPolicies.GeneralPermitLimit; i++)
        {
            using var response = await fixture.Client.GetAsync("/api/v1/universities");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await fixture.Client.GetAsync("/api/v1/universities");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType!.MediaType);
        var problem = (await limited.Content.ReadFromJsonAsync<ApiProblemDetails>())!;
        Assert.Equal("RATE_LIMIT_EXCEEDED", problem.Code);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public void RouteDeclaresAnonymousAccessAndPublicCatalogPolicy()
    {
        var endpoints = fixture.Factory.Services.GetRequiredService<EndpointDataSource>();
        var route = endpoints.Endpoints.OfType<RouteEndpoint>().Single(x => x.RoutePattern.RawText?.Trim('/') == "api/v1/universities");
        Assert.NotNull(route.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Equal(RateLimitPolicies.PublicCatalog, route.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
    }
}
