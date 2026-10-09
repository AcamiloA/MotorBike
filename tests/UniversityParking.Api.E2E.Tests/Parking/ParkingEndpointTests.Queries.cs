using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Api.E2E.Tests.Parking;

public sealed partial class ParkingEndpointTests
{
    [Fact]
    public async Task InsideCountsCoverAllFilteredResults_NotOnlyCurrentPage()
    {
        var teacher = await UserAsync(MemberType.TEACHER);
        var cyclist = await UserAsync();
        var car = await VehicleAsync(teacher, VehicleType.CAR);
        var bicycle = await VehicleAsync(cyclist, VehicleType.BICYCLE);
        await EnterAsync();
        await EnterAsync(car, teacher);
        await EnterAsync(bicycle, cyclist);
        var inside = (await client.GetFromJsonAsync<VehiclesInsideResponse>("/api/v1/parking/inside?pageSize=1"))!;
        Assert.Single(inside.Items);
        Assert.Equal(3, inside.TotalCount);
        Assert.Equal(new ParkingInsideCountsResponse(3, 1, 1, 1), inside.Counts);
        foreach (var search in new[] { target.IdentificationNumber.Value, vehicle.Plate!.Value })
        {
            var filtered = (await client.GetFromJsonAsync<VehiclesInsideResponse>($"/api/v1/parking/inside?search={search}&parkingLotId={lot.Id}&vehicleType=MOTORCYCLE"))!;
            Assert.Equal(vehicle.Id, Assert.Single(filtered.Items).VehicleId);
            Assert.Equal(1, filtered.Counts.Total);
        }
        var history = (await client.GetFromJsonAsync<PagedResponse<ParkingMovementResponse>>($"/api/v1/parking/movements?parkingLotId={lot.Id}&identificationNumber={cyclist.IdentificationNumber.Value}&frameNumber={bicycle.FrameNumber!.Value}&vehicleType=BICYCLE&status=OPEN"))!;
        Assert.Equal(bicycle.Id, Assert.Single(history.Items).VehicleId);
    }
    [Fact]
    public async Task PersonalHistoryCannotReadOtherUsersOrAcceptUserIdOverride()
    {
        var other = await UserAsync();
        var otherVehicle = await VehicleAsync(other);
        await EnterAsync();
        await EnterAsync(otherVehicle, other);
        await LoginAsync(target);
        var mine = (await client.GetFromJsonAsync<PagedResponse<ParkingMovementResponse>>("/api/v1/parking/history/me"))!;
        Assert.Equal(target.Id, Assert.Single(mine.Items).UserId);
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<ParkingMovementResponse>>($"/api/v1/parking/history/me?vehicleId={otherVehicle.Id}"))!.Items);
        await ErrorAsync(await client.GetAsync($"/api/v1/parking/history/me?userId={other.Id}"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Fact]
    public async Task InclusiveBogotaDateFiltersUseUtcBoundariesAndStableDescendingOrder()
    {
        var instants = new[]
        {
            new DateTimeOffset(2026, 10, 7, 4, 59, 59, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 7, 5, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 8, 4, 59, 59, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 8, 5, 0, 0, TimeSpan.Zero)
        };
        await using (var context = fixture.CreateContext())
        {
            foreach (var instant in instants)
            {
                var movement = new ParkingMovement(target.Id, vehicle.Id, lot.Id, zone.Id, instant, guard.Id);
                movement.Close(instant.AddMinutes(2), guard.Id);
                context.ParkingMovements.Add(movement);
            }
            await context.SaveChangesAsync();
        }
        await LoginAsync(target);
        var history = (await client.GetFromJsonAsync<PagedResponse<ParkingMovementResponse>>("/api/v1/parking/history/me?dateFrom=2026-10-07&dateTo=2026-10-07"))!;
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(new[] { instants[2], instants[1] }, history.Items.Select(x => x.CheckInAtUtc));
        await ErrorAsync(await client.GetAsync("/api/v1/parking/history/me?dateFrom=2026-10-08&dateTo=2026-10-07"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Fact]
    public async Task LookupExcludesUnregisteredInactiveVehicles_AndDoesNotApplyLotHours()
    {
        await VehicleAsync(target, registered: false);
        var inactive = await VehicleAsync(target);
        await using (var context = fixture.CreateContext())
        {
            (await context.Vehicles.FindAsync(inactive.Id))!.Deactivate(clock.UtcNow);
            await context.SaveChangesAsync();
        }
        clock.UtcNow = new(2026, 10, 8, 4, 0, 0, TimeSpan.Zero);
        var lookup = (await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(QrPayload: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(target.IdentificationNumber.Value))))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Equal(vehicle.Id, Assert.Single(lookup.EligibleVehicles).Id);
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), HttpStatusCode.Conflict, "PARKING_LOT_CLOSED");
    }
    [Fact]
    public async Task LookupInactiveUserStillShowsOpenMovementForPhysicalExit()
    {
        await EnterAsync();
        await using (var context = fixture.CreateContext())
        {
            (await context.Users.FindAsync(target.Id))!.Deactivate(clock.UtcNow);
            await context.SaveChangesAsync();
        }
        var lookup = (await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(QrPayload: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(target.IdentificationNumber.Value))))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Equal("INACTIVE", lookup.User.Status);
        Assert.NotNull(lookup.CurrentMovement);
        Assert.Empty(lookup.EligibleVehicles);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(await MovementIdAsync(), vehicle.Id))).StatusCode);
    }
    [Fact]
    public async Task ExpiredDocumentsDoNotBlockEntryOrExit()
    {
        await using (var context = fixture.CreateContext())
        {
            context.VehicleDocuments.Add(new(vehicle.Id, VehicleDocumentType.INSURANCE, $"vehicles/{vehicle.Id}/documents/expired.pdf", "expired.pdf",
                "application/pdf", 10, clock.UtcNow.AddDays(-1), issuedOn: new(2020, 1, 1), expiresOn: new(2021, 1, 1)));
            await context.SaveChangesAsync();
        }
        await EnterAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(await MovementIdAsync(), vehicle.Id))).StatusCode);
    }
    [Fact]
    public async Task DifferentGuardsCompetingForSameUserDoNotDeadlock()
    {
        var secondGuard = await UserAsync(MemberType.STAFF, "GUARD");
        var secondVehicle = await VehicleAsync(target, VehicleType.BICYCLE);
        using var otherClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(secondGuard, otherClient);
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), otherClient.PostAsJsonAsync("/api/v1/parking/check-in", Entry(secondVehicle)))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        await ErrorAsync(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "USER_ALREADY_HAS_VEHICLE_INSIDE");
    }
    [Theory]
    [InlineData("user")]
    [InlineData("lot")]
    public async Task EntryAndAdministrativeDeactivationAreSerializedWithoutDeadlock(string kind)
    {
        await using (var context = fixture.CreateContext())
        {
            context.UserRoles.Add(new(guard.Id, (await context.Roles.SingleAsync(x => x.Code == "ADMIN")).Id));
            await context.SaveChangesAsync();
        }
        await LoginAsync(guard);
        var deactivatePath = kind == "user" ? $"/api/v1/users/{target.Id}/deactivate" : $"/api/v1/parking-lots/{lot.Id}/deactivate";
        var entering = client.PostAsJsonAsync("/api/v1/parking/check-in", Entry());
        var deactivating = client.PatchAsync(deactivatePath, null);
        var results = await Task.WhenAll(entering, deactivating).WaitAsync(TimeSpan.FromSeconds(10));
        if (results[0].StatusCode == HttpStatusCode.Created)
            await ErrorAsync(results[1], HttpStatusCode.Conflict, kind == "user" ? "USER_HAS_OPEN_PARKING_MOVEMENT" : "PARKING_LOT_HAS_OPEN_MOVEMENTS");
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, results[1].StatusCode);
            await ErrorAsync(results[0], HttpStatusCode.Conflict, kind == "user" ? "USER_INACTIVE" : "PARKING_LOT_INACTIVE");
        }
    }
    [Theory]
    [InlineData("entry")]
    [InlineData("exit")]
    public async Task FailedAuditRollsBackMovementMutation(string command)
    {
        if (command == "exit") await EnterAsync();
        await using var failingFactory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditLogRepository>();
            services.AddScoped<IAuditLogRepository, InvalidAuditRepository>();
        });
        using var failingClient = failingFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(guard, failingClient);
        var response = command == "entry" ? await failingClient.PostAsJsonAsync("/api/v1/parking/check-in", Entry()) :
            await failingClient.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(await MovementIdAsync(), vehicle.Id));
        await ErrorAsync(response, HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
        await using var context = fixture.CreateContext();
        if (command == "entry") Assert.Empty(await context.ParkingMovements.ToArrayAsync());
        else Assert.Equal(ParkingMovementStatus.OPEN, (await context.ParkingMovements.SingleAsync()).Status);
        Assert.Equal(command == "entry" ? 0 : 1, await context.AuditLogs.CountAsync());
    }
    [Fact]
    public async Task RevokedGuardCannotUseOldTokenToCheckIn()
    {
        await using (var context = fixture.CreateContext())
        {
            var role = await context.Roles.SingleAsync(x => x.Code == "GUARD");
            context.UserRoles.Remove(await context.UserRoles.SingleAsync(x => x.UserId == guard.Id && x.RoleId == role.Id));
            await context.SaveChangesAsync();
        }
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), HttpStatusCode.Forbidden, "FORBIDDEN");
    }
    [Fact]
    public async Task LookupAndSharedCommandRateLimitsAreApplied()
    {
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(QrPayload: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(target.IdentificationNumber.Value))))).StatusCode);
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(QrPayload: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(target.IdentificationNumber.Value)))), HttpStatusCode.TooManyRequests, "RATE_LIMIT_EXCEEDED");
        for (var i = 0; i < 15; i++)
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(await MovementIdAsync(), vehicle.Id))).StatusCode);
        for (var i = 0; i < 15; i++)
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry() with { ParkingLotId = Guid.NewGuid() })).StatusCode);
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry()), HttpStatusCode.TooManyRequests, "RATE_LIMIT_EXCEEDED");
    }
    [Fact]
    public async Task SwaggerAndReadQueryValidationCoverOperationalRoutes()
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/api/v1/parking/access/lookup", "/api/v1/parking/check-in", "/api/v1/parking/check-out",
            "/api/v1/parking/inside", "/api/v1/parking/movements", "/api/v1/parking/history/me" }) Assert.True(paths.TryGetProperty(path, out _));
        Assert.True(paths.GetProperty("/api/v1/parking/check-in").GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
        await ErrorAsync(await client.GetAsync("/api/v1/parking/movements?pageSize=101"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await client.GetAsync("/api/v1/parking/inside?vehicleType=UNKNOWN"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Empty((await client.GetFromJsonAsync<VehiclesInsideResponse>("/api/v1/parking/inside?page=2147483647&pageSize=100"))!.Items);
    }
}
