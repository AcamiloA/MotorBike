using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Infrastructure.Authentication;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Infrastructure.Time;

namespace UniversityParking.Api.E2E.Tests.Parking;

[Collection("Authentication API")]
public sealed partial class ParkingEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;
    private readonly TestClock clock = new();
    private User guard = null!;
    private User target = null!;
    private Vehicle vehicle = null!;
    private AcademicPeriod period = null!;
    private ParkingLot lot = null!;
    private ParkingZone zone = null!;
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        factory = CreateFactory();
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await using var context = fixture.CreateContext();
        context.Roles.AddRange(new Role("USER"), new Role("GUARD"), new Role("ADMIN"));
        await context.SaveChangesAsync();
        guard = await UserAsync(MemberType.STAFF, "GUARD");
        target = await UserAsync(MemberType.STUDENT);
        var before = clock.UtcNow.AddDays(-1);
        period = new("2026-2", new(2026, 7, 1), new(2026, 12, 31), before);
        period.Activate();
        lot = new("Principal", "Kennedy", new(6, 0), new(22, 0), before);
        var zones = Enum.GetValues<VehicleType>().Select(x => new ParkingZone(lot.Id, x.ToString(), x, before)).ToArray();
        zone = zones.Single(x => x.VehicleType == VehicleType.MOTORCYCLE);
        context.AddRange(period, lot);
        context.ParkingZones.AddRange(zones);
        await context.SaveChangesAsync();
        vehicle = await VehicleAsync(target);
        await LoginAsync(guard);
    }
    private WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? extra = null) => fixture.CreateFactory(services =>
    {
        services.AddSingleton<IClock>(clock);
        // Business time is controlled; token validity always follows the real bearer validation clock.
        services.AddScoped<ITokenService>(provider => new JwtTokenService(provider.GetRequiredService<IOptions<JwtOptions>>(), new SystemClock()));
        extra?.Invoke(services);
    });
    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }
    private async Task<User> UserAsync(MemberType member = MemberType.STUDENT, params string[] roles)
    {
        var before = clock.UtcNow.AddDays(-2);
        var user = new User(new IdentificationNumber(Guid.NewGuid().ToString("N")), "Usuario parqueo", "ETITC", member == MemberType.STUDENT ? "Ingeniería" : null,
            member, new CardCode(Guid.NewGuid().ToString("N")), before);
        await using var context = fixture.CreateContext();
        context.Users.Add(user);
        context.UserCredentials.Add(new(user.Id, factory.Services.GetRequiredService<IPasswordHasher>().Hash(AuthApiFixture.Password), before));
        foreach (var code in roles.Append("USER").Distinct())
            context.UserRoles.Add(new(user.Id, (await context.Roles.SingleAsync(x => x.Code == code)).Id));
        await context.SaveChangesAsync();
        return user;
    }
    private async Task<Vehicle> VehicleAsync(User owner, VehicleType type = VehicleType.MOTORCYCLE, bool registered = true)
    {
        var before = clock.UtcNow.AddDays(-1);
        var vehicle = new Vehicle(type, type == VehicleType.BICYCLE ? null : new VehiclePlate(Guid.NewGuid().ToString("N")[..12]),
            type == VehicleType.BICYCLE ? new FrameNumber(Guid.NewGuid().ToString("N")) : null, "Brand", "Model", "Black", before);
        await using var context = fixture.CreateContext();
        context.AddRange(vehicle, new VehicleOwnership(vehicle.Id, owner.Id, before, guard.Id));
        if (registered) context.VehicleRegistrations.Add(new(vehicle.Id, owner.Id, period.Id, before));
        await context.SaveChangesAsync();
        return vehicle;
    }
    private async Task LoginAsync(User user, HttpClient? selected = null)
    {
        selected ??= client;
        var response = await selected.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        selected.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }
    private CheckInVehicleRequest Entry(Vehicle? selected = null, User? owner = null) => new(owner?.Id ?? target.Id, selected?.Id ?? vehicle.Id, lot.Id);
    private async Task<ParkingMovementResponse> EnterAsync(Vehicle? selected = null, User? owner = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry(selected, owner));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ParkingMovementResponse>())!;
    }
    private static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }
    [Fact]
    public async Task RealGuardFlow_LoginLookupEntryInsideExitAndPersonalHistory()
    {
        var lookupResponse = await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(CardCode: target.CardCode.Value));
        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        var lookup = (await lookupResponse.Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Equal(vehicle.Id, Assert.Single(lookup.EligibleVehicles).Id);
        var entered = await EnterAsync();
        Assert.Equal(guard.Id, entered.CheckInGuardId);
        Assert.Equal(zone.Id, entered.ParkingZoneId);
        Assert.Equal(clock.UtcNow, entered.CheckInAtUtc);
        Assert.Equal(TimeSpan.Zero, entered.CheckInAtUtc.Offset);
        var inside = (await client.GetFromJsonAsync<VehiclesInsideResponse>("/api/v1/parking/inside"))!;
        Assert.Equal(entered.MovementId, Assert.Single(inside.Items).MovementId);
        Assert.Equal(1, inside.Counts.Motorcycles);
        lookup = (await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(IdentificationNumber: target.IdentificationNumber.Value))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.NotNull(lookup.CurrentMovement);
        Assert.Empty(lookup.EligibleVehicles);
        clock.UtcNow = clock.UtcNow.AddHours(2);
        var exited = (await (await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(vehicle.Id))).Content.ReadFromJsonAsync<ParkingMovementResponse>())!;
        Assert.Equal("CLOSED", exited.Status);
        Assert.Equal(guard.Id, exited.CheckOutGuardId);
        Assert.Equal(TimeSpan.FromHours(2), exited.Duration);
        Assert.Empty((await client.GetFromJsonAsync<VehiclesInsideResponse>("/api/v1/parking/inside"))!.Items);
        await LoginAsync(target);
        var history = (await client.GetFromJsonAsync<PagedResponse<ParkingMovementResponse>>("/api/v1/parking/history/me"))!;
        Assert.Equal(entered.MovementId, Assert.Single(history.Items).MovementId);
        await using var context = fixture.CreateContext();
        Assert.Equal(new[] { "PARKING_CHECK_IN", "PARKING_CHECK_OUT" }, await context.AuditLogs.OrderBy(x => x.CreatedAt).Select(x => x.Action).ToArrayAsync());
    }
    [Theory]
    [InlineData(10, 59, 59, false)]
    [InlineData(11, 0, 0, true)]
    [InlineData(2, 59, 59, true)]
    [InlineData(3, 0, 0, false)]
    public async Task BogotaOpeningAndClosingBoundariesAreExact(int hour, int minute, int second, bool allowed)
    {
        clock.UtcNow = new(2026, 10, hour < 10 ? 8 : 7, hour, minute, second, TimeSpan.Zero);
        var response = await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry());
        if (allowed) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        else await ErrorAsync(response, HttpStatusCode.Conflict, "PARKING_LOT_CLOSED");
    }
    [Theory]
    [InlineData("user-inactive", "USER_INACTIVE")]
    [InlineData("vehicle-inactive", "VEHICLE_INACTIVE")]
    [InlineData("no-period", "ACADEMIC_PERIOD_NOT_ACTIVE")]
    [InlineData("no-registration", "VEHICLE_REGISTRATION_REQUIRED")]
    [InlineData("registration-cancelled", "VEHICLE_REGISTRATION_CANCELLED")]
    [InlineData("lot-inactive", "PARKING_LOT_INACTIVE")]
    [InlineData("zone-inactive", "PARKING_ZONE_NOT_AVAILABLE")]
    public async Task InvalidEntryNeverCreatesMovementOrAudit(string condition, string code)
    {
        await using (var context = fixture.CreateContext())
        {
            switch (condition)
            {
                case "user-inactive": (await context.Users.FindAsync(target.Id))!.Deactivate(clock.UtcNow); break;
                case "vehicle-inactive": (await context.Vehicles.FindAsync(vehicle.Id))!.Deactivate(clock.UtcNow); break;
                case "no-period": (await context.AcademicPeriods.FindAsync(period.Id))!.Close(); break;
                case "no-registration": context.VehicleRegistrations.Remove(await context.VehicleRegistrations.SingleAsync()); break;
                case "registration-cancelled": (await context.VehicleRegistrations.SingleAsync()).Cancel(clock.UtcNow, guard.Id, "Cancellation"); break;
                case "lot-inactive": (await context.ParkingLots.FindAsync(lot.Id))!.Deactivate(clock.UtcNow); break;
                case "zone-inactive": (await context.ParkingZones.FindAsync(zone.Id))!.Deactivate(clock.UtcNow); break;
            }
            await context.SaveChangesAsync();
        }
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), HttpStatusCode.Conflict, code);
        await using var verification = fixture.CreateContext();
        Assert.Empty(await verification.ParkingMovements.ToArrayAsync());
        Assert.Empty(await verification.AuditLogs.ToArrayAsync());
    }
    [Theory]
    [InlineData("user-inactive")]
    [InlineData("vehicle-inactive")]
    [InlineData("period-closed")]
    [InlineData("registration-cancelled")]
    [InlineData("lot-inactive")]
    [InlineData("outside-hours")]
    public async Task ExitRemainsAllowedWhenEntryConditionsChange(string condition)
    {
        await EnterAsync();
        clock.UtcNow = clock.UtcNow.AddHours(1);
        await using (var context = fixture.CreateContext())
        {
            switch (condition)
            {
                case "user-inactive": (await context.Users.FindAsync(target.Id))!.Deactivate(clock.UtcNow); break;
                case "vehicle-inactive": (await context.Vehicles.FindAsync(vehicle.Id))!.Deactivate(clock.UtcNow); break;
                case "period-closed": (await context.AcademicPeriods.FindAsync(period.Id))!.Close(); break;
                case "registration-cancelled": (await context.VehicleRegistrations.SingleAsync()).Cancel(clock.UtcNow, guard.Id, "Cancellation"); break;
                case "lot-inactive": (await context.ParkingLots.FindAsync(lot.Id))!.Deactivate(clock.UtcNow); break;
                case "outside-hours": clock.UtcNow = new(2026, 10, 8, 4, 0, 0, TimeSpan.Zero); break;
            }
            await context.SaveChangesAsync();
        }
        var response = await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(vehicle.Id));
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(vehicle.Id)), HttpStatusCode.Conflict, "VEHICLE_NOT_INSIDE");
    }
    [Fact]
    public async Task AdminWithoutGuardCannotCheckInOrOut_ButCanLookupAndReadInside()
    {
        var admin = await UserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(admin);
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(vehicle.Id)), HttpStatusCode.Forbidden, "FORBIDDEN");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(CardCode: target.CardCode.Value))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/parking/inside")).StatusCode);
        await using var context = fixture.CreateContext();
        context.UserRoles.Add(new(admin.Id, (await context.Roles.SingleAsync(x => x.Code == "GUARD")).Id));
        await context.SaveChangesAsync();
        await LoginAsync(admin);
        Assert.Equal(admin.Id, (await EnterAsync()).CheckInGuardId);
    }
    [Fact]
    public async Task RegularUserCannotUseOperationalEndpoints_AnonymousCannotEnter()
    {
        await LoginAsync(target);
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(CardCode: target.CardCode.Value)), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await client.GetAsync("/api/v1/parking/inside"), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await client.GetAsync("/api/v1/parking/movements"), HttpStatusCode.Forbidden, "FORBIDDEN");
        client.DefaultRequestHeaders.Authorization = null;
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
    }
    [Fact]
    public async Task LookupValidationMissingResourcesAndStrictBodyFieldsAreProtected()
    {
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest()), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest("card", "id")), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(CardCode: "UNKNOWN")), HttpStatusCode.NotFound, "USER_NOT_FOUND");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", new { userId = target.Id, vehicleId = vehicle.Id, parkingLotId = lot.Id, guardId = Guid.NewGuid() }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-out", new { vehicleId = vehicle.Id, checkOutAt = clock.UtcNow }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(Guid.NewGuid())), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
    }
    [Fact]
    public async Task ConcurrentEntriesForSameVehicleAllowOneMovement()
    {
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        await ErrorAsync(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "VEHICLE_ALREADY_INSIDE");
        await using var context = fixture.CreateContext();
        Assert.Single(await context.ParkingMovements.ToArrayAsync());
        Assert.Single(await context.AuditLogs.ToArrayAsync());
    }
    [Fact]
    public async Task ConcurrentEntriesForDifferentVehiclesSameUserAllowOneMovement()
    {
        var second = await VehicleAsync(target, VehicleType.BICYCLE);
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), client.PostAsJsonAsync("/api/v1/parking/check-in", Entry(second)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        await ErrorAsync(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "USER_ALREADY_HAS_VEHICLE_INSIDE");
        await using var context = fixture.CreateContext();
        Assert.Single(await context.ParkingMovements.ToArrayAsync());
    }
    [Fact]
    public async Task ConcurrentExitsCloseOnceAndAuditOnlyOnce()
    {
        await EnterAsync();
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var request = new CheckOutVehicleRequest(vehicle.Id);
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/parking/check-out", request), client.PostAsJsonAsync("/api/v1/parking/check-out", request));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        await ErrorAsync(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "VEHICLE_NOT_INSIDE");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "PARKING_CHECK_OUT"));
    }
    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 7, 11, 0, 0, TimeSpan.Zero);
    }
    private sealed class InvalidAuditRepository(AppDbContext context) : IAuditLogRepository
    {
        public async Task AddAsync(AuditLog audit, CancellationToken cancellationToken) =>
            await context.AuditLogs.AddAsync(new AuditLog(Guid.NewGuid(), audit.Action, audit.EntityType, audit.EntityId, audit.CreatedAt), cancellationToken);
    }
}
