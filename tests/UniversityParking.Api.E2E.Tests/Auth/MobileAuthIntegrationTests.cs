using Microsoft.AspNetCore.Mvc.Testing;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class MobileAuthIntegrationTests(AuthApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task MobileLoginRestoreAndLogoutUseRealApiJwtAndUserProfile()
    {
        var user = await fixture.CreateUserAsync("USER", "ADMIN");
        var storage = new Storage(); var session = new AuthSession(storage); var nav = new Navigation();
        using var client = fixture.Factory.CreateDefaultClient(new AuthHttpHandler(session, nav, new ApiOptions("https://localhost/")));
        client.BaseAddress = new("https://localhost/");
        var auth = new AuthService(new ApiClient(client), session, nav);
        Assert.True((await auth.LoginAsync(user.IdentificationNumber.Value, AuthApiFixture.Password)).IsSuccess);
        Assert.NotNull(storage.Value); Assert.Equal(user.Id, session.User!.Id); Assert.Equal("ETITC", session.User.University); Assert.Equal(1, nav.HomeCount);
        var restoredSession = new AuthSession(storage); var restoredNav = new Navigation();
        using var restoredClient = fixture.Factory.CreateDefaultClient(new AuthHttpHandler(restoredSession, restoredNav, new ApiOptions("https://localhost/")));
        restoredClient.BaseAddress = new("https://localhost/");
        var restoredAuth = new AuthService(new ApiClient(restoredClient), restoredSession, restoredNav);
        Assert.Equal(StartupState.Authenticated, (await restoredAuth.RestoreAsync()).State);
        await restoredAuth.LogoutAsync(); Assert.Null(storage.Value); Assert.Null(restoredSession.User); Assert.Equal(1, restoredNav.LoginCount);
    }
    [Fact]
    public async Task MobileInvalidLoginAndInvalidJwtReceiveReal401AndClearExpiredSession()
    {
        var user = await fixture.CreateUserAsync("USER"); var storage = new Storage(); var session = new AuthSession(storage); var nav = new Navigation();
        using var client = fixture.Factory.CreateDefaultClient(new AuthHttpHandler(session, nav, new ApiOptions("https://localhost/")));
        client.BaseAddress = new("https://localhost/"); var auth = new AuthService(new ApiClient(client), session, nav);
        var failed = await auth.LoginAsync(user.IdentificationNumber.Value, "IncorrectPassword1");
        Assert.Equal(401, failed.Error!.HttpStatus); Assert.Null(storage.Value); Assert.Equal(0, nav.HomeCount);
        await session.SaveAsync("invalid-jwt", null);
        Assert.Equal(StartupState.Login, (await auth.RestoreAsync()).State); Assert.Null(storage.Value); Assert.Equal(1, nav.LoginCount);
    }
    [Fact]
    public async Task Mobile403FromAdminDashboardPreservesUserSession()
    {
        var user = await fixture.CreateUserAsync("USER"); var storage = new Storage(); var session = new AuthSession(storage); var nav = new Navigation();
        using var client = fixture.Factory.CreateDefaultClient(new AuthHttpHandler(session, nav, new ApiOptions("https://localhost/")));
        client.BaseAddress = new("https://localhost/"); var api = new ApiClient(client); var auth = new AuthService(api, session, nav);
        Assert.True((await auth.LoginAsync(user.IdentificationNumber.Value, AuthApiFixture.Password)).IsSuccess);
        var stored = storage.Value; var result = await api.GetAsync<object>("api/v1/dashboard/admin");
        Assert.Equal(403, result.Error!.HttpStatus); Assert.Equal(stored, storage.Value); Assert.NotNull(session.User); Assert.Equal(0, nav.LoginCount);
    }
    private sealed class Storage : ISecretStorage
    {
        public string? Value;
        public Task<string?> GetAsync(string key) => Task.FromResult(Value);
        public Task SetAsync(string key, string value) { Value = value; return Task.CompletedTask; }
        public void Remove(string key) => Value = null;
    }
    private sealed class Navigation : IAppNavigation
    {
        public int HomeCount, LoginCount;
        public Task ShowLoginAsync(string? message = null) { LoginCount++; return Task.CompletedTask; }
        public Task ShowAuthenticatedAsync() { HomeCount++; return Task.CompletedTask; }
    }
}
