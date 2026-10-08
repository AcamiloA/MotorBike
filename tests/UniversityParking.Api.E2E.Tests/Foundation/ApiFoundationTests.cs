using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http.Headers;
using UniversityParking.Api.RateLimiting;
using UniversityParking.Application.Auth.Login;
using UniversityParking.Application.Common.Results;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Api.Authorization;
using UniversityParking.Domain.Common;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Foundation;

[Collection("Authentication API")]
public sealed class ApiFoundationTests(AuthApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LoginRateLimit_ShouldAllowFiveRequests_ThenReturn429Problem()
    {
        for (var index = 0; index < RateLimitPolicies.LoginPermitLimit; index++)
        {
            var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("missing-user", "WrongPassword1"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        var limited = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("missing-user", "WrongPassword1"));
        await AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, "RATE_LIMIT_EXCEEDED");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task InvalidJson_ShouldReturnValidationProblem_WithoutInternalTypes()
    {
        var response = await fixture.Client.PostAsync("/api/v1/auth/login", new StringContent(
            "{\"identificationNumber\":{},\"password\":\"TestPassword1\"}", Encoding.UTF8, "application/json"));
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.NotEmpty(problem.Errors!);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("System.String", content);
        Assert.DoesNotContain("LoginRequest", content);
        Assert.DoesNotContain("TestPassword1", content);
    }

    [Fact]
    public async Task FluentValidation_ShouldReturnCamelCaseFieldErrors()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("", ""));
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Contains("identificationNumber", problem.Errors!.Keys);
        Assert.Contains("password", problem.Errors.Keys);
    }

    [Fact]
    public async Task MissingRoute_ShouldReturnConsistentProblem()
    {
        var response = await fixture.Client.GetAsync("/api/v1/nonexistent");
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task UnexpectedException_ShouldReturnSafe500_EvenInDevelopment()
    {
        await using var factory = fixture.CreateFactory(services => services.Replace(
            ServiceDescriptor.Transient<IRequestHandler<LoginCommand, Result<LoginResult>>, ThrowingLoginHandler>()));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("001A", "TestPassword1"));
        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("TEST_SECRET", content);
        Assert.DoesNotContain("InvalidOperationException", content);
        Assert.DoesNotContain("StackTrace", content);
    }

    [Fact]
    public async Task Health_ShouldCheckRealPostgres_WithoutExposingConnectionDetails()
    {
        var response = await fixture.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
        Assert.DoesNotContain("Password", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnreachableDatabase_ShouldReturn503Problem()
    {
        await using var factory = fixture.CreateFactory(settings: new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=unreachable_test;Username=test;Timeout=1"
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/health");
        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "SERVICE_UNAVAILABLE");
        Assert.DoesNotContain("unreachable_test", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Swagger_ShouldDescribeAuthContracts_BearerAndProtectedOperations()
    {
        await using var factory = fixture.CreateFactory(settings: new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        var paths = root.GetProperty("paths");
        var login = paths.GetProperty("/api/v1/auth/login").GetProperty("post");
        Assert.False(login.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
        Assert.Contains(login.GetProperty("tags").EnumerateArray(), tag => tag.GetString() == "Auth");
        var change = paths.GetProperty("/api/v1/auth/change-password").GetProperty("post");
        Assert.NotEmpty(change.GetProperty("security").EnumerateArray());
        Assert.True(login.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
    }

    [Fact]
    public async Task SwaggerDisabled_ShouldNotExposeDocumentation()
    {
        await using var factory = fixture.CreateFactory(settings: new Dictionary<string, string?> { ["Swagger:Enabled"] = "false" });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await AssertProblemAsync(await client.GetAsync("/swagger/v1/swagger.json"), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    [Theory]
    [InlineData("validation", HttpStatusCode.BadRequest, "VALIDATION_ERROR")]
    [InlineData("conflict", HttpStatusCode.Conflict, "INVALID_NEWS_STATE")]
    public async Task DomainException_ShouldUseFunctionalProblemSemantics(string input, HttpStatusCode status, string code)
    {
        await using var factory = fixture.CreateFactory(services => services.Replace(
            ServiceDescriptor.Transient<IRequestHandler<LoginCommand, Result<LoginResult>>, DomainFailingLoginHandler>()));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(input, "TestPassword1"));
        var problem = await AssertProblemAsync(response, status, code);
        if (status == HttpStatusCode.BadRequest) Assert.NotEmpty(problem.Errors!);
    }

    [Fact]
    public async Task AuthorizationMiddleware_ShouldWrite403ProblemForRoleDenial()
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User);
        await using var factory = fixture.CreateFactory(services => services.PostConfigure<AuthorizationOptions>(options =>
            options.AddPolicy(PolicyNames.Authenticated, policy => policy.RequireAuthenticatedUser().RequireRole(RoleCodes.Admin))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(AuthApiFixture.Password, "DifferentPassword2"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        await AssertProblemAsync(await client.SendAsync(request), HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    private static async Task<ApiProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<ApiProblemDetails>())!;
        Assert.Equal(code, problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.Equal((int)status, problem.Status);
        return problem;
    }

    public sealed class ThrowingLoginHandler : IRequestHandler<LoginCommand, Result<LoginResult>>
    {
        public Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("TEST_SECRET_CONNECTION_STRING");
    }

    public sealed class DomainFailingLoginHandler : IRequestHandler<LoginCommand, Result<LoginResult>>
    {
        public Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken) =>
            throw new DomainException(request.IdentificationNumber == "validation" ? "VALIDATION_ERROR" : "INVALID_NEWS_STATE", "Mensaje de dominio.");
    }
}
