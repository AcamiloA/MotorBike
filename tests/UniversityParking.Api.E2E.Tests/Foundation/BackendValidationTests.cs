using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Users;

namespace UniversityParking.Api.E2E.Tests.Foundation;

[Collection("Authentication API")]
public sealed class BackendValidationTests(AuthApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task AdminCreatesStudent_WhoLogsInAndHasOnlyOwnAccess()
    {
        var admin = await fixture.CreateUserAsync("USER", "ADMIN");
        await Login(admin.IdentificationNumber.Value);
        var identification = Guid.NewGuid().ToString("N");
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/users", new { identificationNumber = identification,
            fullName = "Estudiante integral", universityId = UniversityParking.Domain.Universities.UniversityIds.Etitc, career = "Ingeniería", memberType = "STUDENT", cardCode = "CARD-INTEGRAL", initialPassword = AuthApiFixture.Password });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<UserCreatedResponse>())!.Id;
        await Login(identification);
        var me = (await fixture.Client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me"))!;
        Assert.Equal(id, me.Id); Assert.Equal("STUDENT", me.MemberType); Assert.Equal(new[] { "USER" }, me.Roles);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/v1/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/v1/dashboard/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/health")).StatusCode);
        await using var context = fixture.CreateContext();
        var audit = await context.AuditLogs.SingleAsync(x => x.Action == "USER_CREATED" && x.EntityId == id);
        Assert.DoesNotContain(AuthApiFixture.Password, audit.NewValues!);
        Assert.DoesNotContain("passwordHash", audit.NewValues!, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task DefaultFixtureStorageIsTemporaryPrivateAndDoesNotExposeKeys()
    {
        var storage = fixture.Factory.Services.GetRequiredService<IFileStorage>();
        var key = "validation/" + Guid.NewGuid().ToString("N") + ".pdf";
        await using var bytes = new MemoryStream("%PDF-1.7 validation"u8.ToArray());
        await storage.UploadAsync(new FileUpload(key, bytes, "application/pdf", bytes.Length), default);
        await using var read = await storage.OpenReadAsync(key, default);
        Assert.Equal(bytes.Length, read.Length); Assert.Null(await storage.GetReadUrlAsync(key, default));
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/" + key)).StatusCode);
    }
    private async Task Login(string identification)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(identification, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }
}
