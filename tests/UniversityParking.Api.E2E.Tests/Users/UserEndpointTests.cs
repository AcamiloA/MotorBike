using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Domain.Auditing;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.E2E.Tests.Users;

[Collection("Authentication API")]
public sealed class UserEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private HttpClient Client => fixture.Client;
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateContext();
        context.Roles.AddRange(new Role("USER"), new Role("GUARD"), new Role("ADMIN"));
        await context.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private static CreateUserRequest Request(string? identification = null, string? card = null) =>
        new(identification ?? Guid.NewGuid().ToString("N"), "Estudiante nuevo", "ETITC", "Ingeniería",
            UserMemberType.STUDENT, card ?? Guid.NewGuid().ToString("N"), "NewPassword1");
    private async Task<User> AuthenticateAsync(params string[] roles)
    {
        var user = await fixture.CreateUserAsync(roles.Distinct().ToArray());
        await LoginAsync(user.IdentificationNumber.Value, AuthApiFixture.Password);
        return user;
    }
    private async Task LoginAsync(string identification, string password)
    {
        var response = await Client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(identification, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }
    private async Task<Guid> CreateAsync(CreateUserRequest? request = null)
    {
        var response = await Client.PostJsonAsync("/api/v1/users", request ?? Request());
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<UserCreatedResponse>())!.Id;
        Assert.EndsWith(id.ToString(), response.Headers.Location!.ToString());
        return id;
    }
    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }
    [Fact]
    public async Task AdminCreatesStudent_WithCredentialMandatoryUserRoleAndAudit_StudentCanLogin()
    {
        var admin = await AuthenticateAsync("USER", "ADMIN");
        var request = Request("00001234", "STUDENT-CARD");
        var id = await CreateAsync(request);
        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync(x => x.Id == id);
        Assert.Equal(MemberType.STUDENT, user.MemberType);
        var credential = await context.UserCredentials.SingleAsync(x => x.UserId == id);
        var hasher = fixture.Factory.Services.GetRequiredService<IPasswordHasher>();
        Assert.NotEqual(request.InitialPassword, credential.PasswordHash);
        Assert.True(hasher.Verify(request.InitialPassword, credential.PasswordHash));
        var audit = await context.AuditLogs.SingleAsync(x => x.EntityId == id);
        Assert.Equal(admin.Id, audit.ActorUserId);
        Assert.Equal("USER_CREATED", audit.Action);
        Assert.DoesNotContain("Password", audit.NewValues!);
        await LoginAsync(request.IdentificationNumber, request.InitialPassword);
        var profile = (await Client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me"))!;
        Assert.Equal(id, profile.Id);
        Assert.Equal("00001234", profile.IdentificationNumber);
        Assert.Equal(new[] { "USER" }, profile.Roles);
        var raw = await (await Client.GetAsync("/api/v1/users/me")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("USER")]
    [InlineData("GUARD")]
    public async Task ManagementEndpoints_RejectNonAdmins(string role)
    {
        await AuthenticateAsync("USER", role);
        var id = Guid.NewGuid();
        foreach (var request in new[]
        {
            new HttpRequestMessage(HttpMethod.Get, "/api/v1/users"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/v1/users/{id}"),
            new HttpRequestMessage(HttpMethod.Post, "/api/v1/users") { Content = JsonContent.Create(Request()) },
            new HttpRequestMessage(HttpMethod.Put, $"/api/v1/users/{id}") { Content = JsonContent.Create(new UpdateUserRequest("Name", "ETITC", null, UserMemberType.STAFF, "card")) },
            new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{id}/activate"),
            new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{id}/deactivate"),
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/users/{id}/roles") { Content = JsonContent.Create(new AssignRoleRequest("ADMIN")) },
            new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/users/{id}/roles/GUARD")
        })
        {
            await AssertError(await Client.SendAsync(request), HttpStatusCode.Forbidden, "FORBIDDEN");
            request.Dispose();
        }
        // These denied requests did not create any additional account or audit.
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }
    [Fact]
    public async Task AnonymousCannotCreateOrReadProfile()
    {
        await AssertError(await Client.PostJsonAsync("/api/v1/users", Request()), HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
        await AssertError(await Client.GetAsync("/api/v1/users/me"), HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateIdentifiers_ReturnConflictWithoutPartialWrites(bool identification)
    {
        await AuthenticateAsync("USER", "ADMIN");
        var request = Request();
        await CreateAsync(request);
        var duplicate = identification ? Request(" " + request.IdentificationNumber + " ") : Request(card: " " + request.CardCode + " ");
        await AssertError(await Client.PostJsonAsync("/api/v1/users", duplicate), HttpStatusCode.Conflict,
            identification ? "USER_ALREADY_EXISTS" : "USER_CARD_CODE_ALREADY_EXISTS");
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Users.CountAsync());
        Assert.Equal(2, await context.UserCredentials.CountAsync());
        Assert.Equal(1, await context.AuditLogs.CountAsync());
    }
    [Theory]
    [InlineData("career")]
    [InlineData("password")]
    [InlineData("role")]
    [InlineData("memberType")]
    public async Task CreateValidationRejectsInvalidInput(string field)
    {
        await AuthenticateAsync("USER", "ADMIN");
        var request = Request();
        object input = field switch
        {
            "career" => request with { Career = " " },
            "password" => request with { InitialPassword = "weak" },
            "role" => request with { Roles = ["ROOT"] },
            _ => new { request.IdentificationNumber, request.FullName, request.University, request.Career,
                memberType = "INVALID", request.CardCode, request.InitialPassword }
        };
        await AssertError(await Client.PostJsonAsync("/api/v1/users", input), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }
    [Fact]
    public async Task ProfileUpdate_ChangesOnlyAllowedFields_RejectsPrivilegeEscalation()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var request = Request();
        await CreateAsync(request);
        await LoginAsync(request.IdentificationNumber, request.InitialPassword);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutJsonAsync("/api/v1/users/me", new UpdateMyProfileRequest("Nombre cambiado", "Sistemas"))).StatusCode);
        var profile = (await Client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me"))!;
        Assert.Equal("Nombre cambiado", profile.FullName);
        Assert.Equal(request.University, profile.University);
        await AssertError(await Client.PutJsonAsync("/api/v1/users/me", new { fullName = "Attack", career = "Sistemas", roles = new[] { "ADMIN" }, memberType = "STAFF" }),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await AssertError(await Client.PutJsonAsync("/api/v1/users/me", new UpdateMyProfileRequest("Attack", null)), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        profile = (await Client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me"))!;
        Assert.Equal("Nombre cambiado", profile.FullName);
        Assert.Equal("STUDENT", profile.MemberType);
        Assert.Equal(new[] { "USER" }, profile.Roles);
    }
    [Fact]
    public async Task StatusChanges_AreIdempotentAndAuditedOnlyOnce()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var id = await CreateAsync();
        foreach (var action in new[] { "deactivate", "deactivate", "activate", "activate" })
            Assert.Equal(HttpStatusCode.NoContent, (await Client.PatchAsync($"/api/v1/users/{id}/{action}", null)).StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(UserStatus.ACTIVE, (await context.Users.FindAsync(id))!.Status);
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "USER_ACTIVATED"));
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "USER_DEACTIVATED"));
    }
    [Fact]
    public async Task Roles_AreIdempotent_UserMandatory_AndProfileReturnsCurrentDatabaseRoles()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var request = Request();
        var id = await CreateAsync(request);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await Client.PostJsonAsync($"/api/v1/users/{id}/roles", new AssignRoleRequest("GUARD"))).StatusCode);
        await AssertError(await Client.DeleteAsync($"/api/v1/users/{id}/roles/USER"), HttpStatusCode.Conflict, "REQUIRED_ROLE_CANNOT_BE_REMOVED");
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.UserRoles.CountAsync(x => x.UserId == id));
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/v1/users/{id}/roles/GUARD")).StatusCode);
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "ROLE_ASSIGNED"));
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "ROLE_REMOVED"));
        await LoginAsync(request.IdentificationNumber, request.InitialPassword);
        Assert.Equal(new[] { "USER" }, (await Client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me"))!.Roles);
    }
    [Fact]
    public async Task AdminUpdate_AuditsOldAndNewCard_IdentificationCannotBeChanged()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var request = Request();
        var id = await CreateAsync(request);
        var update = new UpdateUserRequest("Docente", "Nueva universidad", null, UserMemberType.TEACHER, "NEW-CARD");
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutJsonAsync($"/api/v1/users/{id}", update)).StatusCode);
        var profile = (await Client.GetFromJsonAsync<UserProfileResponse>($"/api/v1/users/{id}"))!;
        Assert.Equal("TEACHER", profile.MemberType);
        Assert.Equal(request.IdentificationNumber, profile.IdentificationNumber);
        await using var context = fixture.CreateContext();
        var audit = await context.AuditLogs.SingleAsync(x => x.EntityId == id && x.Action == "USER_UPDATED");
        Assert.Contains(request.CardCode, audit.OldValues!);
        Assert.Contains("NEW-CARD", audit.NewValues!);
        await AssertError(await Client.PutJsonAsync($"/api/v1/users/{id}", new { fullName = "Wrong", university = "ETITC", memberType = "STAFF", cardCode = "NEW-CARD", identificationNumber = "hacked" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Fact]
    public async Task SearchFilters_PaginateAndSearchNamesIdentifiersAndCards()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var id = await CreateAsync(Request("ID-SEARCH-000", "CARD-SEARCH-000") with { Roles = ["GUARD"] });
        await CreateAsync();
        foreach (var search in new[] { "nuevo", "id-search", "card-search" })
        {
            var page = (await Client.GetFromJsonAsync<PagedResponse<UserListItemResponse>>($"/api/v1/users?search={search}&memberType=STUDENT&status=ACTIVE&role=GUARD&pageSize=1"))!;
            Assert.Equal(id, Assert.Single(page.Items).Id);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(1, page.TotalPages);
        }
        var second = (await Client.GetFromJsonAsync<PagedResponse<UserListItemResponse>>("/api/v1/users?memberType=STUDENT&page=2&pageSize=1"))!;
        Assert.Single(second.Items);
        Assert.Equal(2, second.TotalCount);
        var empty = (await Client.GetFromJsonAsync<PagedResponse<UserListItemResponse>>("/api/v1/users?page=2147483647&pageSize=100"))!;
        Assert.Empty(empty.Items);
        await AssertError(await Client.GetAsync("/api/v1/users?pageSize=101"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Fact]
    public async Task UnknownUsers_Return404ForAllAdministrativeCommands()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var id = Guid.NewGuid();
        await AssertError(await Client.GetAsync($"/api/v1/users/{id}"), HttpStatusCode.NotFound, "USER_NOT_FOUND");
        await AssertError(await Client.PatchAsync($"/api/v1/users/{id}/deactivate", null), HttpStatusCode.NotFound, "USER_NOT_FOUND");
        await AssertError(await Client.PostJsonAsync($"/api/v1/users/{id}/roles", new AssignRoleRequest("GUARD")), HttpStatusCode.NotFound, "USER_NOT_FOUND");
    }
    [Fact]
    public async Task RemovedAdministratorCannotManageUsersWithOldToken()
    {
        var admin = await AuthenticateAsync("USER", "ADMIN");
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/v1/users/{admin.Id}/roles/ADMIN")).StatusCode);
        await AssertError(await Client.GetAsync("/api/v1/users"), HttpStatusCode.Forbidden, "FORBIDDEN");
        Assert.Equal(new[] { "USER" }, (await Client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me"))!.Roles);
    }
    [Fact]
    public async Task DeactivateBlockedByOpenMovement_WithoutChangingUserOrAudit()
    {
        var admin = await AuthenticateAsync("USER", "ADMIN");
        var id = await CreateAsync();
        await using var context = fixture.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var vehicle = new Vehicle(VehicleType.MOTORCYCLE, new VehiclePlate("ABC123"), null, "Brand", "Model", "Black", now);
        var lot = new ParkingLot("Test lot", "Kennedy", new(6, 0), new(22, 0), now);
        var zone = new ParkingZone(lot.Id, "Motos", VehicleType.MOTORCYCLE, now);
        context.AddRange(vehicle, lot, zone, new ParkingMovement(id, vehicle.Id, lot.Id, zone.Id, now, admin.Id));
        await context.SaveChangesAsync();
        await AssertError(await Client.PatchAsync($"/api/v1/users/{id}/deactivate", null), HttpStatusCode.Conflict, "USER_HAS_OPEN_PARKING_MOVEMENT");
        Assert.Equal(UserStatus.ACTIVE, (await context.Users.FindAsync(id))!.Status);
        Assert.False(await context.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action == "USER_DEACTIVATED"));
    }
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public async Task StudentChangeChecksOnlyCurrentlyOwnedActiveCars(bool activeCar, bool currentOwnership, bool allowed)
    {
        var admin = await AuthenticateAsync("USER", "ADMIN");
        var id = await CreateAsync(Request() with { MemberType = UserMemberType.TEACHER, Career = null });
        await using var context = fixture.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var car = new Vehicle(VehicleType.CAR, new VehiclePlate("ABC123"), null, "Brand", "Model", "Black", now);
        if (!activeCar) car.Deactivate(now);
        var ownership = new VehicleOwnership(car.Id, id, now, admin.Id);
        if (!currentOwnership) ownership.Close(now, "Transfer test");
        context.AddRange(car, ownership);
        await context.SaveChangesAsync();
        var response = await Client.PutJsonAsync($"/api/v1/users/{id}", new UpdateUserRequest("Estudiante", "ETITC", "Ingeniería", UserMemberType.STUDENT, "NEW-CARD"));
        if (allowed) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        else await AssertError(response, HttpStatusCode.Conflict, "INVALID_MEMBER_TYPE_CHANGE");
        var user = await context.Users.FindAsync(id);
        Assert.Equal(allowed ? MemberType.STUDENT : MemberType.TEACHER, user!.MemberType);
        Assert.Equal(allowed ? 1 : 0, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "USER_UPDATED"));
    }
    [Fact]
    public async Task ConcurrentDuplicateCreation_OnlyOneAccountCredentialAndAuditPersists()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var request = Request();
        var responses = await Task.WhenAll(Client.PostJsonAsync("/api/v1/users", request), Client.PostJsonAsync("/api/v1/users", request with { CardCode = "OTHER-CARD" }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        await AssertError(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "USER_ALREADY_EXISTS");
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Users.CountAsync());
        Assert.Equal(2, await context.UserCredentials.CountAsync());
        Assert.Equal(1, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRoleAssignment_IsIdempotentAndAuditsOnlyOnce()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var id = await CreateAsync();
        var responses = await Task.WhenAll(Client.PostJsonAsync($"/api/v1/users/{id}/roles", new AssignRoleRequest("GUARD")),
            Client.PostJsonAsync($"/api/v1/users/{id}/roles", new AssignRoleRequest("GUARD")));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.UserRoles.CountAsync(x => x.UserId == id));
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "ROLE_ASSIGNED"));
    }

    [Fact]
    public async Task ConcurrentDeactivation_IsIdempotentAndAuditsOnlyOnce()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var id = await CreateAsync();
        var responses = await Task.WhenAll(Client.PatchAsync($"/api/v1/users/{id}/deactivate", null),
            Client.PatchAsync($"/api/v1/users/{id}/deactivate", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "USER_DEACTIVATED"));
    }

    [Fact]
    public async Task CreationRollsBackAccountCredentialAndRoles_WhenAuditInsertFails()
    {
        var admin = await AuthenticateAsync("USER", "ADMIN");
        await using var factory = fixture.CreateFactory(services =>
        {
            services.RemoveAll<IAuditLogRepository>();
            services.AddScoped<IAuditLogRepository, InvalidActorAuditRepository>();
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var loginResponse = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(admin.IdentificationNumber.Value, AuthApiFixture.Password));
        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        await AssertError(await client.PostJsonAsync("/api/v1/users", Request()), HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, await context.UserCredentials.CountAsync());
        Assert.Equal(2, await context.UserRoles.CountAsync());
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SwaggerDocumentsAllUserRoutesAndStringMemberTypes()
    {
        using var document = JsonDocument.Parse(await Client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/api/v1/users", "/api/v1/users/me", "/api/v1/users/{id}",
            "/api/v1/users/{id}/activate", "/api/v1/users/{id}/deactivate", "/api/v1/users/{id}/roles", "/api/v1/users/{id}/roles/{role}" })
            Assert.True(paths.TryGetProperty(path, out _));
        var operation = paths.GetProperty("/api/v1/users").GetProperty("post");
        Assert.True(operation.TryGetProperty("security", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("201", out _));
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("UserMemberType");
        Assert.Equal("string", schema.GetProperty("type").GetString());
    }

    [Fact]
    public async Task MemberTypeCannotBeOmittedOrProvidedAsInteger()
    {
        await AuthenticateAsync("USER", "ADMIN");
        var request = Request();
        await AssertError(await Client.PostJsonAsync("/api/v1/users", new { request.IdentificationNumber,
            request.FullName, request.University, request.Career, request.CardCode, request.InitialPassword }),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await AssertError(await Client.PostJsonAsync("/api/v1/users", new { request.IdentificationNumber,
            request.FullName, request.University, request.Career, request.CardCode, request.InitialPassword, memberType = 0 }),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    private sealed class InvalidActorAuditRepository(AppDbContext context) : IAuditLogRepository
    {
        public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken) =>
            await context.AuditLogs.AddAsync(new AuditLog(Guid.NewGuid(), auditLog.Action, auditLog.EntityType,
                auditLog.EntityId, auditLog.CreatedAt), cancellationToken);
    }
}

internal static class UserTestJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string uri, T value) =>
        client.PostAsJsonAsync(uri, value, Options);
    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string uri, T value) =>
        client.PutAsJsonAsync(uri, value, Options);
}
