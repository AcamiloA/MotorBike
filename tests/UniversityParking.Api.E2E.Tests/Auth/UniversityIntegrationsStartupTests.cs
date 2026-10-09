using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class UniversityIntegrationsStartupTests(AuthApiFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync(); await using var db = fixture.CreateContext();
        db.Roles.Add(new Role("USER")); await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private static Dictionary<string, string?> Settings(Guid id, bool enabled = false) => new()
    {
        ["UniversityIntegrations:Universities:0:UniversityId"] = id.ToString(),
        ["UniversityIntegrations:Universities:0:Enabled"] = enabled.ToString()
    };

    [Theory][InlineData(false, "PENDING")][InlineData(true, "ACTIVE")]
    public async Task DisabledRegisteredAdapterDoesNotChangePublicRegistration(bool auto, string status)
    {
        var adapter = new TestAdapter(UniversityIds.Cmc);
        var settings = Settings(adapter.UniversityId); settings["StudentRegistration:AutoApprove"] = auto.ToString();
        await using var factory = fixture.CreateFactory(s => s.AddSingleton<IUniversityStudentValidator>(adapter), settings);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var request = new RegisterStudentRequest(Guid.NewGuid().ToString("N"), "Estudiante", UniversityIds.Cmc,
            "Carrera", Guid.NewGuid().ToString("N"), AuthApiFixture.Password);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register/student", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<RegisterStudentResponse>())!;
        Assert.Equal(status, result.Status); Assert.Equal(0, adapter.Calls);
        await using var db = fixture.CreateContext(); var user = await db.Users.SingleAsync();
        Assert.Equal(status, user.Status.ToString()); Assert.Equal(MemberType.STUDENT, user.MemberType);
        Assert.Equal(1, await db.UserCredentials.CountAsync()); Assert.Equal(1, await db.UserRoles.CountAsync());
        Assert.Equal("STUDENT_REGISTERED", (await db.AuditLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task EnabledMissingProviderPreventsStartup()
    {
        await using var factory = fixture.CreateFactory(settings: Settings(UniversityIds.Etitc, true));
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("sin proveedor", error.Message);
    }

    [Fact]
    public async Task DuplicateProvidersPreventStartupEvenWithoutConfiguration()
    {
        await using var factory = fixture.CreateFactory(s =>
        {
            s.AddSingleton<IUniversityStudentValidator>(new TestAdapter(UniversityIds.Etitc));
            s.AddSingleton<IUniversityStudentValidator>(new TestAdapter(UniversityIds.Etitc));
        });
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("duplicado", error.Message);
    }

    [Fact]
    public async Task UnknownCatalogIdPreventsStartupEvenWhenDisabled()
    {
        await using var factory = fixture.CreateFactory(settings: Settings(Guid.NewGuid()));
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("fuera del catálogo", error.Message);
    }

    private sealed class TestAdapter(Guid id) : IUniversityStudentValidator
    {
        public Guid UniversityId => id;
        public int Calls { get; private set; }
        public Task<UniversityStudentValidationResult> ValidateAsync(UniversityStudentValidationRequest request, CancellationToken token)
        { Calls++; throw new InvalidOperationException("A disabled integration must never be called."); }
    }
}
