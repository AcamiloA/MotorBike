using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class MobileUserFeatureIntegrationTests(AuthApiFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync(); var admin = await fixture.CreateUserAsync("USER", "ADMIN");
        await using var context = fixture.CreateContext();
        var period = new AcademicPeriod("2026-2", new(2026,7,1), new(2026,12,31), DateTimeOffset.UtcNow); period.Activate();
        var news = new NewsItem("Aviso real", "Contenido publicado", admin.Id, DateTimeOffset.UtcNow); news.Publish(DateTimeOffset.UtcNow);
        context.AddRange(period, news); await context.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private async Task<User> CreateUser(string member)
    {
        await using var context = fixture.CreateContext(); var now = DateTimeOffset.UtcNow;
        var user = new User(new IdentificationNumber(Guid.NewGuid().ToString("N")), "Usuario móvil", UniversityParking.Domain.Universities.UniversityIds.Etitc, member == "STUDENT" ? "Ingeniería" : null,
            Enum.Parse<MemberType>(member), new CardCode(Guid.NewGuid().ToString("N")), now);
        context.Users.Add(user); context.UserCredentials.Add(new(user.Id, fixture.Factory.Services.GetRequiredService<IPasswordHasher>().Hash(AuthApiFixture.Password), now));
        context.UserRoles.Add(new(user.Id, (await context.Roles.SingleAsync(x => x.Code == "USER")).Id)); await context.SaveChangesAsync(); return user;
    }
    private async Task<(UserApiService Api, HttpClient Client, AuthService Auth)> Login(User user)
    {
        var session = new AuthSession(new Storage()); var nav = new Navigation();
        var client = fixture.Factory.CreateDefaultClient(new AuthHttpHandler(session, nav, new ApiOptions("https://localhost/"))); client.BaseAddress = new("https://localhost/");
        var api = new ApiClient(client); var auth = new AuthService(api, session, nav);
        Assert.True((await auth.LoginAsync(user.IdentificationNumber.Value, AuthApiFixture.Password)).IsSuccess); return (new(api), client, auth);
    }
    private static VehicleRegistrationInput Registration(string type) => new(type, type == "BICYCLE" ? "FRAME-001" : "ABC123", "Brand", "Model", "Black",
        new("photo.png", "image/png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aG6kAAAAASUVORK5CYII=")));
    [Theory]
    [InlineData("STUDENT", "MOTORCYCLE", true)] [InlineData("STUDENT", "BICYCLE", true)]
    [InlineData("STUDENT", "CAR", false)] [InlineData("TEACHER", "CAR", true)]
    public async Task MobileMultipartAndMemberRestrictionsUseRealBackend(string member, string type, bool allowed)
    {
        var user = await CreateUser(member); var setup = await Login(user); using var client = setup.Client;
        var result = await setup.Api.RegisterAsync(Registration(type));
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed) { Assert.Equal("STUDENT_CANNOT_REGISTER_CAR", result.Error!.Code); return; }
        var id = result.Value!.Id; var mine = await setup.Api.MyVehiclesAsync(); Assert.Equal(id, Assert.Single(mine.Value!).Id);
        var detail = await setup.Api.VehicleAsync(id); Assert.Equal(user.Id, detail.Value!.Vehicle.CurrentOwnerId);
        var photo = await setup.Api.FileAsync(detail.Value.VerificationImage!.ContentUrl); Assert.True(photo.IsSuccess); Assert.NotEmpty(photo.Value!);
        var edited = await setup.Api.EditVehicleAsync(id, "Updated", "Model", "Red"); Assert.True(edited.IsSuccess);
        Assert.Equal("UPDATED", (await setup.Api.VehicleAsync(id)).Value!.Vehicle.Brand);
        Assert.True((await setup.Api.VehicleStatusAsync(id, false)).IsSuccess); Assert.True((await setup.Api.VehicleStatusAsync(id, true)).IsSuccess);
    }
    [Fact]
    public async Task MobilePersonalNewsHistoryProfilePasswordAndRenewalWorkAgainstPostgres()
    {
        var user = await CreateUser("STUDENT"); var setup = await Login(user); using var client = setup.Client;
        var vehicle = (await setup.Api.RegisterAsync(Registration("BICYCLE"))).Value!.Id;
        Assert.Equal("PUBLISHED", Assert.Single((await setup.Api.NewsAsync()).Value!.Items).Status);
        Assert.Empty((await setup.Api.HistoryAsync(new(2026,1,1), new(2026,12,31), vehicle)).Value!.Items);
        Assert.True((await setup.Api.EditProfileAsync("Nombre nuevo", "Carrera nueva")).IsSuccess);
        Assert.Equal("Nombre nuevo", (await setup.Api.ProfileAsync()).Value!.FullName);
        Assert.True((await setup.Api.ChangePasswordAsync(AuthApiFixture.Password, "NewPassword2")).IsSuccess);
        Assert.True((await setup.Auth.LoginAsync(user.IdentificationNumber.Value, "NewPassword2")).IsSuccess);
        await using (var context = fixture.CreateContext())
        {
            (await context.AcademicPeriods.SingleAsync()).Close(); var next = new AcademicPeriod("2027-1", new(2027,1,1), new(2027,6,30), DateTimeOffset.UtcNow); next.Activate(); context.AcademicPeriods.Add(next); await context.SaveChangesAsync();
        }
        var renewal = await setup.Api.RenewAsync(vehicle, []);
        Assert.True(renewal.IsSuccess, $"{renewal.Error?.HttpStatus} {renewal.Error?.Code}: {renewal.Error?.Message}");
        await using var verification = fixture.CreateContext(); Assert.Single(await verification.Vehicles.ToListAsync()); Assert.Equal(2, await verification.VehicleRegistrations.CountAsync());
        var other = await Login(await CreateUser("STUDENT")); using var otherClient = other.Client;
        var denial = await other.Api.VehicleAsync(vehicle); Assert.False(denial.IsSuccess); Assert.Equal(404, denial.Error!.HttpStatus);
    }
    private sealed class Storage : ISecretStorage
    { private string? value; public Task<string?> GetAsync(string key) => Task.FromResult(value); public Task SetAsync(string key, string token) { value = token; return Task.CompletedTask; } public void Remove(string key) => value = null; }
    private sealed class Navigation : IAppNavigation
    { public Task ShowLoginAsync(string? message = null) => Task.CompletedTask; public Task ShowAuthenticatedAsync() => Task.CompletedTask; }
}
