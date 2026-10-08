using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniversityParking.Api.Authorization;
using UniversityParking.Contracts.Auth;
using UniversityParking.Domain.Users;
using UniversityParking.Infrastructure.Authentication;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class AuthApiTests(AuthApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<LoginResponse> LoginAsync(string identification)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(identification, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    [Fact]
    public async Task ValidLogin_ShouldReturnSignedJwtAndMinimalSession()
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User, RoleCodes.Admin);
        var login = await LoginAsync(" " + user.IdentificationNumber.Value + " ");
        Assert.Equal(user.Id, login.User.Id);
        Assert.Contains(RoleCodes.Admin, login.User.Roles);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal("STAFF", token.Claims.Single(x => x.Type == "member_type").Value);
        var serialized = JsonSerializer.Serialize(login);
        Assert.DoesNotContain("passwordHash", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AuthApiFixture.Password, serialized);
    }

    [Fact]
    public async Task InvalidLogin_ShouldHideWhetherUserExists()
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User);
        foreach (var identification in new[] { user.IdentificationNumber.Value, "nonexistent" })
        {
            var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(identification, "WrongPassword1"));
            await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
        }
    }

    [Fact]
    public async Task InactiveUser_ShouldNotReceiveToken()
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User);
        await using var context = fixture.CreateContext();
        var stored = await context.Users.SingleAsync(x => x.Id == user.Id);
        stored.Deactivate(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, AuthApiFixture.Password));
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "AUTH_USER_INACTIVE");
    }

    [Fact]
    public async Task ChangePassword_ShouldRequireToken()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/change-password", new ChangePasswordRequest(AuthApiFixture.Password, "DifferentPassword2"));
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task ChangePassword_ShouldPersistNewHash_AndInvalidateOldPassword()
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User);
        var login = await LoginAsync(user.IdentificationNumber.Value);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(AuthApiFixture.Password, "DifferentPassword2"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var changed = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        var old = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, AuthApiFixture.Password));
        await AssertProblemAsync(old, HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
        var current = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, "DifferentPassword2"));
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        await using var context = fixture.CreateContext();
        var credential = await context.UserCredentials.SingleAsync(x => x.UserId == user.Id);
        Assert.NotEqual("DifferentPassword2", credential.PasswordHash);
        Assert.DoesNotContain("DifferentPassword2", credential.PasswordHash);
    }

    [Theory]
    [InlineData("WrongPassword1", "DifferentPassword2", "AUTH_INVALID_CREDENTIALS")]
    [InlineData(AuthApiFixture.Password, "lowercase1", "VALIDATION_ERROR")]
    [InlineData(AuthApiFixture.Password, AuthApiFixture.Password, "VALIDATION_ERROR")]
    public async Task ChangePassword_ShouldRejectWrongCurrentPasswordOrInvalidNewPassword(string current, string next, string code)
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User);
        var login = await LoginAsync(user.IdentificationNumber.Value);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(current, next))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        await AssertProblemAsync(await fixture.Client.SendAsync(request), HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task InvalidBearer_ShouldReturnUnauthorizedProblem()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(AuthApiFixture.Password, "DifferentPassword2"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        await AssertProblemAsync(await fixture.Client.SendAsync(request), HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminRole_ShouldNotImplyGuardCapability(bool hasGuard)
    {
        var user = await fixture.CreateUserAsync(hasGuard ? [RoleCodes.User, RoleCodes.Admin, RoleCodes.Guard] : [RoleCodes.User, RoleCodes.Admin]);
        var login = await LoginAsync(user.IdentificationNumber.Value);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(token.Claims, "Bearer", "sub", "role"));
        using var scope = fixture.Factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        Assert.True((await authorization.AuthorizeAsync(principal, null, PolicyNames.Admin)).Succeeded);
        Assert.Equal(hasGuard, (await authorization.AuthorizeAsync(principal, null, PolicyNames.Guard)).Succeeded);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, content.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(content.RootElement.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signing-key")]
    [InlineData("missing-expiration")]
    [InlineData("invalid-subject")]
    public async Task BearerValidation_ShouldRejectInvalidTokenProperties(string invalidProperty)
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User);
        var settings = fixture.Factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var now = DateTimeOffset.UtcNow.UtcDateTime;
        var issuedAt = invalidProperty == "expired" ? now.AddHours(-9) : now;
        DateTime? expiresAt = invalidProperty == "missing-expiration" ? null : invalidProperty == "expired" ? now.AddHours(-1) : now.AddHours(8);
        var key = invalidProperty == "signing-key" ? new byte[32] : Encoding.UTF8.GetBytes(settings.Key);
        var token = new JwtSecurityToken(
            invalidProperty == "issuer" ? "wrong-issuer" : settings.Issuer,
            invalidProperty == "audience" ? "wrong-audience" : settings.Audience,
            [new Claim("sub", invalidProperty == "invalid-subject" ? "invalid-user-id" : user.Id.ToString()), new Claim("role", RoleCodes.User)],
            issuedAt, expiresAt, new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(AuthApiFixture.Password, "DifferentPassword2"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        await AssertProblemAsync(await fixture.Client.SendAsync(request), HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task UserDeactivatedAfterLogin_ShouldNotChangePasswordWithOldToken()
    {
        var user = await fixture.CreateUserAsync(RoleCodes.User);
        var login = await LoginAsync(user.IdentificationNumber.Value);
        await using var context = fixture.CreateContext();
        var stored = await context.Users.SingleAsync(x => x.Id == user.Id);
        stored.Deactivate(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(AuthApiFixture.Password, "DifferentPassword2"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        await AssertProblemAsync(await fixture.Client.SendAsync(request), HttpStatusCode.Conflict, "USER_INACTIVE");
    }
}
