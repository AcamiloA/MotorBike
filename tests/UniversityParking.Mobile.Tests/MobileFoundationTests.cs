using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed class MobileFoundationTests
{
    private static UserProfileResponse User(params string[] roles) => new(Guid.NewGuid(), "123", "Camilo", new Guid("a1100000-0000-4000-8000-000000000001"), "ETITC", "Ingeniería", "STUDENT", "CARD", "ACTIVE", roles.Length == 0 ? ["USER"] : roles);
    private static ApiClient Client(AuthSession session, Navigation navigation, Handler transport) => new(new HttpClient(
        new AuthHttpHandler(session, navigation, new ApiOptions("https://test.example/")) { InnerHandler = transport }) { BaseAddress = new("https://test.example/") });
    private static HttpResponseMessage Ok(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    [Theory]
    [InlineData("ACCOUNT_PENDING","Tu registro está pendiente de aprobación.","Tu registro está pendiente de aprobación.")]
    [InlineData("ACCOUNT_REJECTED","Tu solicitud de registro no fue aprobada.","Tu solicitud de registro no fue aprobada.")]
    [InlineData("AUTH_INVALID_CREDENTIALS","Información privada","Identificación o contraseña incorrectas.")]
    [InlineData("AUTH_USER_INACTIVE","El usuario está inactivo.","Identificación o contraseña incorrectas.")]
    public async Task LoginDisplaysTrustedRegistrationErrorWithoutCreatingSession(string code,string detail,string expected)
    {
        var storage=new Storage();var session=new AuthSession(storage);var nav=new Navigation();
        var transport=new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {Content=JsonContent.Create(new ApiProblemDetails{Status=401,Code=code,Detail=detail})}));
        var vm=new UniversityParking.Mobile.ViewModels.LoginViewModel(new AuthService(Client(session,nav,transport),session,nav))
        {IdentificationNumber="001",Password="Password1"};
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Equal(expected,vm.ErrorMessage);Assert.Equal("",vm.Password);Assert.Null(storage.Value);Assert.Null(session.User);
        Assert.Equal(0,nav.HomeCount);Assert.Equal(1,transport.Count);
    }
    [Fact]
    public async Task PublicRegistrationSendsNoBearerAndDoesNotCreateSession()
    {
        var storage=new Storage();var session=new AuthSession(storage);var nav=new Navigation();
        var transport=new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
        {Content=JsonContent.Create(new RegisterStudentResponse(Guid.NewGuid(),"ACTIVE"))}));
        var result=await new StudentRegistrationApiService(Client(session,nav,transport)).RegisterAsync(new("001","Nombre",Guid.NewGuid(),"Carrera","CARD","Password1"));
        Assert.True(result.IsSuccess);Assert.Null(Assert.Single(transport.Headers));Assert.Null(storage.Value);Assert.Null(session.User);Assert.Equal(0,nav.HomeCount);
    }
    [Fact]
    public async Task RegistrationNeverAttachesAnExistingBearerOrOverwritesIt()
    {
        var storage=new Storage();var session=new AuthSession(storage);await session.SaveAsync("existing",User());var nav=new Navigation();
        var transport=new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
        {Content=JsonContent.Create(new RegisterStudentResponse(Guid.NewGuid(),"PENDING"))}));
        var result=await new StudentRegistrationApiService(Client(session,nav,transport)).RegisterAsync(new("001","Nombre",Guid.NewGuid(),"Carrera","CARD","Password1"));
        Assert.True(result.IsSuccess);Assert.Null(Assert.Single(transport.Headers));Assert.Equal("existing",storage.Value);Assert.Equal(0,nav.HomeCount);
    }
    [Fact]
    public async Task LoginPersistsTokenValidatesMeAndNavigates_WithNoAuthorizationOnLogin()
    {
        var storage = new Storage(); var session = new AuthSession(storage); var nav = new Navigation(); var user = User();
        var transport = new Handler((request, _) => Task.FromResult(request.Method == HttpMethod.Post ?
            Ok(new LoginResponse("jwt-test", DateTimeOffset.UtcNow.AddHours(1), new(user.Id, user.FullName, user.MemberType, user.Roles))) : Ok(user)));
        var auth = new AuthService(Client(session, nav, transport), session, nav);
        Assert.True((await auth.LoginAsync(" 123 ", "TestPassword1")).IsSuccess);
        Assert.Equal("jwt-test", storage.Value); Assert.Equal(user.Id, session.User!.Id); Assert.Equal(user.Roles, session.User.Roles); Assert.Equal(1, nav.HomeCount);
        Assert.Null(transport.Headers[0]); Assert.Equal("Bearer jwt-test", transport.Headers[1]); Assert.Equal(2, transport.Count);
    }
    [Fact]
    public async Task RestartLoadsPersistedTokenAndValidatesProfile()
    {
        var storage = new Storage { Value = "saved" }; var session = new AuthSession(storage); var nav = new Navigation(); var user = User("USER", "GUARD", "ADMIN");
        var transport = new Handler((_, _) => Task.FromResult(Ok(user)));
        var result = await new AuthService(Client(session, nav, transport), session, nav).RestoreAsync();
        Assert.Equal(StartupState.Authenticated, result.State); Assert.Equal(user.Id, session.User!.Id); Assert.Equal(user.Roles, session.User.Roles); Assert.Equal("Bearer saved", Assert.Single(transport.Headers));
        Assert.Equal(new[] { "USER", "GUARD", "ADMIN" }, RoleNavigation.Areas(user.Roles));
    }
    [Fact]
    public async Task NoTokenNavigatesToLoginWithoutRequest()
    {
        var session = new AuthSession(new Storage()); var nav = new Navigation(); var transport = new Handler((_, _) => throw new InvalidOperationException("Unexpected HTTP request"));
        Assert.Equal(StartupState.Login, (await new AuthService(Client(session, nav, transport), session, nav).RestoreAsync()).State);
        Assert.Equal(0, transport.Count); Assert.Equal(1, nav.LoginCount);
    }
    [Fact]
    public async Task UnauthorizedStartupClearsSecureStorageAndSession()
    {
        var storage = new Storage { Value = "expired" }; var session = new AuthSession(storage); var nav = new Navigation();
        var transport = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") }));
        var result = await new AuthService(Client(session, nav, transport), session, nav).RestoreAsync();
        Assert.Equal(StartupState.Login, result.State); Assert.Null(storage.Value); Assert.Null(session.User); Assert.Equal(1, nav.LoginCount); Assert.Equal(1, transport.Count);
    }
    [Fact]
    public async Task Concurrent401OnlyClearsAndNavigatesOnce()
    {
        var session = new AuthSession(new Storage()); await session.SaveAsync("old", User()); var nav = new Navigation();
        var allSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        var transport = new Handler(async (_, _) => { if (Interlocked.Increment(ref count) == 3) allSent.SetResult(); await allSent.Task; return new(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") }; });
        var api = Client(session, nav, transport);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => api.GetAsync<UserProfileResponse>("api/v1/users/me"))).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, nav.LoginCount); Assert.Null(await session.GetTokenAsync());
    }
    [Fact]
    public async Task OldUnauthorizedResponseCannotInvalidateNewLogin()
    {
        var session = new AuthSession(new Storage()); await session.SaveAsync("old", User()); var nav = new Navigation();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new Handler(async (_, _) => { sent.SetResult(); await reply.Task; return new(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") }; });
        var request = Client(session, nav, transport).GetAsync<UserProfileResponse>("api/v1/users/me"); await sent.Task;
        await session.SaveAsync("new", User()); reply.SetResult(); await request;
        Assert.Equal("new", await session.GetTokenAsync()); Assert.Equal(0, nav.LoginCount);
    }
    [Fact]
    public async Task ForbiddenDoesNotClearValidTokenAndUsesSpanishMessage()
    {
        var session = new AuthSession(new Storage { Value = "valid" }); var nav = new Navigation();
        var transport = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = JsonContent.Create(new ApiProblemDetails { Status = 403, Code = "FORBIDDEN", Detail = "internal" }) }));
        var result = await Client(session, nav, transport).GetAsync<UserProfileResponse>("api/v1/users/me");
        Assert.Equal("valid", await session.GetTokenAsync()); Assert.Equal(0, nav.LoginCount); Assert.Equal("No tienes permisos para realizar esta acción.", result.Error!.Message); Assert.Equal(1, transport.Count);
    }
    [Fact]
    public async Task NetworkFailurePreservesTokenAndGetRetriesOnlyOnce()
    {
        var storage = new Storage { Value = "valid" }; var session = new AuthSession(storage); var nav = new Navigation();
        var transport = new Handler((_, _) => throw new HttpRequestException("sensitive diagnostics"));
        var result = await new AuthService(Client(session, nav, transport), session, nav).RestoreAsync();
        Assert.Equal(StartupState.Unavailable, result.State); Assert.Equal("valid", storage.Value); Assert.Equal(2, transport.Count);
        Assert.Equal(0, nav.LoginCount); Assert.DoesNotContain("sensitive", result.Message!);
    }
    [Theory]
    [InlineData(408)] [InlineData(502)] [InlineData(503)] [InlineData(504)]
    public async Task TransientGetRetriesOnceThenReturnsSuccess(int status)
    {
        var session = new AuthSession(new Storage()); var nav = new Navigation();
        var transport = new Handler((_, count) => Task.FromResult(count == 1 ? new HttpResponseMessage((HttpStatusCode)status) : Ok(User())));
        Assert.True((await Client(session, nav, transport).GetAsync<UserProfileResponse>("api/v1/users/me")).IsSuccess); Assert.Equal(2, transport.Count);
    }
    [Fact]
    public async Task FailedPostIsNeverRetriedAndPasswordsAreClearedByViewModel()
    {
        var session = new AuthSession(new Storage()); var nav = new Navigation(); var transport = new Handler((_, _) => throw new HttpRequestException());
        var vm = new LoginViewModel(new AuthService(Client(session, nav, transport), session, nav)) { IdentificationNumber = "123", Password = "TestPassword1" };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Equal(1, transport.Count); Assert.Empty(vm.Password); Assert.False(vm.IsBusy); Assert.Contains("conectar", vm.ErrorMessage);
    }
    [Fact]
    public async Task InvalidCredentialsNeverAttachOrInvalidateExistingToken()
    {
        var session = new AuthSession(new Storage { Value = "existing" }); var nav = new Navigation();
        var transport = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = JsonContent.Create(new ApiProblemDetails { Status = 401, Code = "AUTH_INVALID_CREDENTIALS" }) }));
        var result = await new AuthService(Client(session, nav, transport), session, nav).LoginAsync("123", "wrong");
        Assert.False(result.IsSuccess); Assert.Equal("existing", await session.GetTokenAsync()); Assert.Null(Assert.Single(transport.Headers)); Assert.Equal(0, nav.LoginCount);
    }
    [Fact]
    public async Task LogoutClearsPersistentAndMemoryStateAndNavigates()
    {
        var storage = new Storage(); var session = new AuthSession(storage); await session.SaveAsync("token", User()); var nav = new Navigation();
        await new AuthService(Client(session, nav, new Handler((_, _) => throw new InvalidOperationException())), session, nav).LogoutAsync();
        Assert.Null(storage.Value); Assert.Null(session.User); Assert.Null(await session.GetTokenAsync()); Assert.Equal(1, nav.LoginCount);
    }
    [Fact]
    public async Task CancellationIsNotConvertedIntoNetworkFailureOrRetried()
    {
        var session = new AuthSession(new Storage()); var nav = new Navigation(); var transport = new Handler((_, _) => throw new OperationCanceledException());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(session, nav, transport).GetAsync<UserProfileResponse>("api/v1/users/me", cancellation.Token));
        Assert.Equal(0, nav.LoginCount);
    }
    [Fact]
    public void ProductionUrlRejectsHttpCredentialsAndQueryStrings()
    {
        Assert.Throws<ArgumentException>(() => new ApiOptions("http://10.0.2.2:5197"));
        Assert.Throws<ArgumentException>(() => new ApiOptions("https://user:password@test.example"));
        Assert.Throws<ArgumentException>(() => new ApiOptions("https://test.example?secret=x"));
        Assert.Equal("http://10.0.2.2:5197/", new ApiOptions("http://10.0.2.2:5197", true).BaseAddress.AbsoluteUri);
    }
    [Fact]
    public async Task AuthenticationNeverSendsTokenToDifferentOrigin()
    {
        var session = new AuthSession(new Storage { Value = "token" }); var nav = new Navigation(); var transport = new Handler((_, _) => Task.FromResult(Ok(User())));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Client(session, nav, transport).GetAsync<UserProfileResponse>("https://other.example/api/v1/users/me"));
        Assert.Equal(0, transport.Count);
    }
    [Fact]
    public async Task SecureStorageFailureCannotProduceAuthenticatedNavigation()
    {
        var storage = new Storage { FailWrite = true }; var session = new AuthSession(storage); var nav = new Navigation(); var user = User();
        var transport = new Handler((_, _) => Task.FromResult(Ok(new LoginResponse("token", DateTimeOffset.UtcNow.AddHours(1), new(user.Id, user.FullName, user.MemberType, user.Roles)))));
        var result = await new AuthService(Client(session, nav, transport), session, nav).LoginAsync("123", "password");
        Assert.Equal("SESSION_STORAGE_ERROR", result.Error!.Code); Assert.Equal(0, nav.HomeCount); Assert.Null(await session.GetTokenAsync());
    }
    [Fact]
    public async Task DelayedStartupCannotRestoreProfileAfterLogout()
    {
        var session = new AuthSession(new Storage { Value = "valid" }); var nav = new Navigation(); var user = User();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new Handler(async (_, _) => { sent.SetResult(); await reply.Task; return Ok(user); });
        var auth = new AuthService(Client(session, nav, transport), session, nav);
        var restore = auth.RestoreAsync(); await sent.Task; await auth.LogoutAsync(); reply.SetResult(); await restore;
        Assert.Null(session.User); Assert.Null(await session.GetTokenAsync()); Assert.Equal(0, nav.HomeCount);
    }
    [Fact]
    public async Task DoubleTapLoginDoesNotSendTwoPosts()
    {
        var session = new AuthSession(new Storage()); var nav = new Navigation(); var user = User();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new Handler(async (request, _) =>
        {
            if (request.Method == HttpMethod.Get) return Ok(user);
            sent.SetResult(); await reply.Task;
            return Ok(new LoginResponse("token", DateTimeOffset.UtcNow.AddHours(1), new(user.Id, user.FullName, user.MemberType, user.Roles)));
        });
        var vm = new LoginViewModel(new AuthService(Client(session, nav, transport), session, nav)) { IdentificationNumber = "123", Password = "TestPassword1" };
        var first = vm.SignInCommand.ExecuteAsync(null); await sent.Task;
        await vm.SignInCommand.ExecuteAsync(null); Assert.Equal(1, transport.Count);
        reply.SetResult(); await first; Assert.Equal(2, transport.Count); Assert.Equal(1, nav.HomeCount);
    }
    [Fact]
    public async Task InvalidJsonHasSafeErrorWithoutClearingToken()
    {
        var session = new AuthSession(new Storage { Value = "token" }); var nav = new Navigation();
        var transport = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("sensitive invalid payload") }));
        var result = await Client(session, nav, transport).GetAsync<UserProfileResponse>("api/v1/users/me");
        Assert.Equal("INVALID_RESPONSE", result.Error!.Code); Assert.Equal("token", await session.GetTokenAsync()); Assert.DoesNotContain("sensitive", result.Error.Message);
    }
    private sealed class Storage : ISecretStorage
    {
        public string? Value; public bool FailWrite;
        public Task<string?> GetAsync(string key) => Task.FromResult(Value);
        public Task SetAsync(string key, string value) { if (FailWrite) throw new IOException(); Value = value; return Task.CompletedTask; }
        public void Remove(string key) => Value = null;
    }
    private sealed class Navigation : IAppNavigation
    {
        public int LoginCount, HomeCount;
        public Task ShowLoginAsync(string? message = null) { Interlocked.Increment(ref LoginCount); return Task.CompletedTask; }
        public Task ShowAuthenticatedAsync() { HomeCount++; return Task.CompletedTask; }
    }
    private sealed class Handler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Count; public List<string?> Headers { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { var count = Interlocked.Increment(ref Count); lock (Headers) Headers.Add(request.Headers.Authorization?.ToString()); return send(request, count); }
    }
}
