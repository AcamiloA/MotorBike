using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Infrastructure.Persistence.Repositories;

namespace UniversityParking.Api.E2E.Tests.Administration;

[Collection("Authentication API")]
public sealed class AdministrationEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private HttpClient Client => fixture.Client;
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static CreateAcademicPeriodRequest Period(string name = "2027-1") => new(name, new(2027, 1, 15), new(2027, 6, 30));
    private static CreateParkingLotRequest Lot(string name = "Principal", string campus = "Kennedy") => new(name, campus, new(6, 0), new(22, 0));
    private async Task<User> LoginAsync(params string[] roles)
    {
        var user = await fixture.CreateUserAsync(roles.Append("USER").Distinct().ToArray());
        await LoginExistingAsync(Client, user);
        return user;
    }
    private static async Task LoginExistingAsync(HttpClient client, User user)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }
    private async Task<Guid> CreatePeriodAsync(string name = "2027-1")
    {
        var response = await Client.PostAsJsonAsync("/api/v1/academic-periods", Period(name));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AcademicPeriodCreatedResponse>())!.Id;
    }
    private async Task<Guid> CreateLotAsync(string name = "Principal", string campus = "Kennedy")
    {
        var response = await Client.PostAsJsonAsync("/api/v1/parking-lots", Lot(name, campus));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ParkingLotCreatedResponse>())!.Id;
    }
    private static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }
    [Fact]
    public async Task PeriodLifecycleIsIdempotentAndDoesNotAutomaticallyActivateNext()
    {
        await LoginAsync("USER", "ADMIN");
        var first = await CreatePeriodAsync();
        var second = await CreatePeriodAsync("2027-2");
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.AcademicPeriods.CountAsync(x => x.Status == AcademicPeriodStatus.PLANNED));
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"/api/v1/academic-periods/{first}/activate", null)).StatusCode);
        await ErrorAsync(await Client.PostAsync($"/api/v1/academic-periods/{second}/activate", null), HttpStatusCode.Conflict, "ACTIVE_ACADEMIC_PERIOD_ALREADY_EXISTS");
        var current = (await Client.GetFromJsonAsync<AcademicPeriodResponse>("/api/v1/academic-periods/current"))!;
        Assert.Equal(first, current.Id);
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"/api/v1/academic-periods/{first}/close", null)).StatusCode);
        await ErrorAsync(await Client.PostAsync($"/api/v1/academic-periods/{first}/activate", null), HttpStatusCode.Conflict, "ACADEMIC_PERIOD_NOT_ACTIVE");
        await ErrorAsync(await Client.GetAsync("/api/v1/academic-periods/current"), HttpStatusCode.NotFound, "ACADEMIC_PERIOD_NOT_ACTIVE");
        Assert.Equal(AcademicPeriodStatus.PLANNED, (await context.AcademicPeriods.FindAsync(second))!.Status);
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "ACADEMIC_PERIOD_ACTIVATED"));
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "ACADEMIC_PERIOD_CLOSED"));
    }
    [Theory]
    [InlineData("USER")]
    [InlineData("GUARD")]
    public async Task NonAdminsCannotManagePeriodsOrLots(string role)
    {
        await LoginAsync(role);
        var id = Guid.NewGuid();
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/academic-periods", Period()), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await Client.GetAsync("/api/v1/academic-periods"), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await Client.PostAsync($"/api/v1/academic-periods/{id}/activate", null), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await Client.PostAsync($"/api/v1/academic-periods/{id}/close", null), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/parking-lots", Lot()), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await Client.PutAsJsonAsync($"/api/v1/parking-lots/{id}", new UpdateParkingLotRequest("Name", "Campus", new(6, 0), new(22, 0))), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await Client.PatchAsync($"/api/v1/parking-lots/{id}/activate", null), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await Client.PatchAsync($"/api/v1/parking-lots/{id}/deactivate", null), HttpStatusCode.Forbidden, "FORBIDDEN");
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }
    [Fact]
    public async Task CurrentPeriodRequiresAuthenticationButIsAvailableToNormalUser()
    {
        await ErrorAsync(await Client.GetAsync("/api/v1/academic-periods/current"), HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
        await LoginAsync("USER", "ADMIN");
        var id = await CreatePeriodAsync();
        await Client.PostAsync($"/api/v1/academic-periods/{id}/activate", null);
        await LoginAsync("USER");
        Assert.Equal(id, (await Client.GetFromJsonAsync<AcademicPeriodResponse>("/api/v1/academic-periods/current"))!.Id);
    }
    [Fact]
    public async Task DuplicatePeriodAndLotNamesReturnStableConflicts()
    {
        await LoginAsync("USER", "ADMIN");
        await CreatePeriodAsync();
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/academic-periods", Period(" 2027-1 ")), HttpStatusCode.Conflict, "ACADEMIC_PERIOD_ALREADY_EXISTS");
        await CreateLotAsync();
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/parking-lots", Lot(" Principal ", " Kennedy ")), HttpStatusCode.Conflict, "PARKING_LOT_ALREADY_EXISTS");
        await CreateLotAsync(campus: "Otra sede");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AcademicPeriods.CountAsync());
        Assert.Equal(2, await context.ParkingLots.CountAsync());
        Assert.Equal(6, await context.ParkingZones.CountAsync());
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidPeriodDatesReturn400WithoutPersisting(int days)
    {
        await LoginAsync("USER", "ADMIN");
        var request = Period();
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/academic-periods", request with { EndsOn = request.StartsOn.AddDays(days) }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.AcademicPeriods.ToListAsync());
    }
    [Theory]
    [InlineData(22, 6)]
    [InlineData(6, 6)]
    public async Task OvernightAndEqualSchedulesAreRejected(int opening, int closing)
    {
        await LoginAsync("USER", "ADMIN");
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/parking-lots", Lot() with { OpeningTime = new(opening, 0), ClosingTime = new(closing, 0) }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.ParkingLots.ToListAsync());
        Assert.Empty(await context.ParkingZones.ToListAsync());
    }
    [Fact]
    public async Task CreationRejectsMissingDatesTimesAndInjectedStatusOrZones()
    {
        await LoginAsync("USER", "ADMIN");
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/academic-periods", new { name = "Missing dates" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/academic-periods", new { name = "Injected", startsOn = "2027-01-01", endsOn = "2027-06-30", status = "ACTIVE" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/parking-lots", new { name = "Missing time", campus = "Kennedy", closingTime = "22:00:00" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/parking-lots", new { name = "Injected", campus = "Kennedy", openingTime = "06:00:00", closingTime = "22:00:00", zones = new[] { "CUSTOM" } }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Fact]
    public async Task CreationGeneratesThreeStandardZonesAndUpdatePreservesThem()
    {
        await LoginAsync("USER", "ADMIN");
        var id = await CreateLotAsync();
        await using var context = fixture.CreateContext();
        var originalZones = await context.ParkingZones.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(3, originalZones.Count);
        Assert.Equal(3, originalZones.Select(x => x.VehicleType).Distinct().Count());
        Assert.All(originalZones, x => Assert.Equal(ParkingZoneStatus.ACTIVE, x.Status));
        var response = await Client.PutAsJsonAsync($"/api/v1/parking-lots/{id}", new UpdateParkingLotRequest("Nuevo nombre", "Nueva sede", new(7, 0), new(21, 0)));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var zones = await context.ParkingZones.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(originalZones.Select(x => (x.Id, x.VehicleType)), zones.Select(x => (x.Id, x.VehicleType)));
        var list = (await Client.GetFromJsonAsync<PagedResponse<ParkingLotResponse>>("/api/v1/parking-lots"))!;
        Assert.Equal("Nuevo nombre", Assert.Single(list.Items).Name);
        Assert.Equal(3, list.Items[0].Zones.Count);
        Assert.Equal(new TimeOnly(7, 0), list.Items[0].OpeningTime);
    }
    [Fact]
    public async Task GuardCannotSeeInactiveLotsEvenWhenRequestingInactiveStatus()
    {
        await LoginAsync("USER", "ADMIN");
        var active = await CreateLotAsync();
        var inactive = await CreateLotAsync("Otro");
        await Client.PatchAsync($"/api/v1/parking-lots/{inactive}/deactivate", null);
        var all = (await Client.GetFromJsonAsync<PagedResponse<ParkingLotResponse>>("/api/v1/parking-lots"))!;
        Assert.Equal(2, all.TotalCount);
        var filtered = (await Client.GetFromJsonAsync<PagedResponse<ParkingLotResponse>>("/api/v1/parking-lots?status=INACTIVE"))!;
        Assert.Equal(inactive, Assert.Single(filtered.Items).Id);
        await LoginAsync("USER", "GUARD");
        foreach (var path in new[] { "/api/v1/parking-lots", "/api/v1/parking-lots?status=INACTIVE" })
        {
            var guarded = (await Client.GetFromJsonAsync<PagedResponse<ParkingLotResponse>>(path))!;
            Assert.Equal(active, Assert.Single(guarded.Items).Id);
            Assert.Equal(1, guarded.TotalCount);
        }
        await LoginAsync("USER");
        await ErrorAsync(await Client.GetAsync("/api/v1/parking-lots"), HttpStatusCode.Forbidden, "FORBIDDEN");
    }
    [Fact]
    public async Task LotStatusOperationsAreIdempotentAndDoNotReactivateInactiveZones()
    {
        await LoginAsync("USER", "ADMIN");
        var id = await CreateLotAsync();
        await using (var context = fixture.CreateContext())
        {
            (await context.ParkingZones.FirstAsync()).Deactivate(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }
        foreach (var action in new[] { "deactivate", "deactivate", "activate", "activate" })
            Assert.Equal(HttpStatusCode.NoContent, (await Client.PatchAsync($"/api/v1/parking-lots/{id}/{action}", null)).StatusCode);
        await using var verification = fixture.CreateContext();
        Assert.Equal(1, await verification.ParkingZones.CountAsync(x => x.Status == ParkingZoneStatus.INACTIVE));
        Assert.Equal(1, await verification.AuditLogs.CountAsync(x => x.Action == "PARKING_LOT_ACTIVATED"));
        Assert.Equal(1, await verification.AuditLogs.CountAsync(x => x.Action == "PARKING_LOT_DEACTIVATED"));
    }
    [Fact]
    public async Task LotDeactivationIsBlockedByOpenMovement()
    {
        var admin = await LoginAsync("USER", "ADMIN");
        var id = await CreateLotAsync();
        await using (var context = fixture.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            var vehicle = new Vehicle(VehicleType.CAR, new VehiclePlate("ABC123"), null, "Brand", "Model", "Color", now);
            context.AddRange(vehicle, new ParkingMovement(admin.Id, vehicle.Id, id,
                (await context.ParkingZones.SingleAsync(x => x.ParkingLotId == id && x.VehicleType == VehicleType.CAR)).Id, now, admin.Id));
            await context.SaveChangesAsync();
        }
        await ErrorAsync(await Client.PatchAsync($"/api/v1/parking-lots/{id}/deactivate", null), HttpStatusCode.Conflict, "PARKING_LOT_HAS_OPEN_MOVEMENTS");
        await using var verification = fixture.CreateContext();
        Assert.Equal(ParkingLotStatus.ACTIVE, (await verification.ParkingLots.FindAsync(id))!.Status);
        Assert.False(await verification.AuditLogs.AnyAsync(x => x.Action == "PARKING_LOT_DEACTIVATED"));
    }
    [Fact]
    public async Task ClosingPeriodPreservesRegistrationAndOwnership_ExpiredIsDerived()
    {
        var admin = await LoginAsync("USER", "ADMIN");
        var id = await CreatePeriodAsync();
        await Client.PostAsync($"/api/v1/academic-periods/{id}/activate", null);
        Guid vehicleId;
        await using (var context = fixture.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            var vehicle = new Vehicle(VehicleType.CAR, new VehiclePlate("ABC123"), null, "Brand", "Model", "Color", now);
            vehicleId = vehicle.Id;
            context.AddRange(vehicle, new VehicleOwnership(vehicle.Id, admin.Id, now, admin.Id), new VehicleRegistration(vehicle.Id, admin.Id, id, now));
            await context.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"/api/v1/academic-periods/{id}/close", null)).StatusCode);
        await using var verification = fixture.CreateContext();
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, (await verification.VehicleRegistrations.SingleAsync()).Status);
        Assert.Null((await verification.VehicleOwnerships.SingleAsync()).EndAt);
        Assert.Equal("EXPIRED", (await Client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{vehicleId}"))!.Vehicle.RegistrationState);
    }
    [Fact]
    public async Task ConcurrentActivationsLeaveExactlyOneActivePeriodAndOneAudit()
    {
        await LoginAsync("USER", "ADMIN");
        var first = await CreatePeriodAsync();
        var second = await CreatePeriodAsync("2027-2");
        var responses = await Task.WhenAll(Client.PostAsync($"/api/v1/academic-periods/{first}/activate", null), Client.PostAsync($"/api/v1/academic-periods/{second}/activate", null));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        await ErrorAsync(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.NoContent), HttpStatusCode.Conflict, "ACTIVE_ACADEMIC_PERIOD_ALREADY_EXISTS");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AcademicPeriods.CountAsync(x => x.Status == AcademicPeriodStatus.ACTIVE));
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "ACADEMIC_PERIOD_ACTIVATED"));
    }
    [Fact]
    public async Task ConcurrentClosingSamePeriodIsIdempotent()
    {
        await LoginAsync("USER", "ADMIN");
        var id = await CreatePeriodAsync();
        await Client.PostAsync($"/api/v1/academic-periods/{id}/activate", null);
        var responses = await Task.WhenAll(Client.PostAsync($"/api/v1/academic-periods/{id}/close", null), Client.PostAsync($"/api/v1/academic-periods/{id}/close", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "ACADEMIC_PERIOD_CLOSED"));
    }
    [Fact]
    public async Task ConcurrentDuplicateLotCreationLeavesOneLotThreeZonesAndOneAudit()
    {
        await LoginAsync("USER", "ADMIN");
        var responses = await Task.WhenAll(Client.PostAsJsonAsync("/api/v1/parking-lots", Lot()), Client.PostAsJsonAsync("/api/v1/parking-lots", Lot()));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        await ErrorAsync(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "PARKING_LOT_ALREADY_EXISTS");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.ParkingLots.CountAsync());
        Assert.Equal(3, await context.ParkingZones.CountAsync());
        Assert.Equal(1, await context.AuditLogs.CountAsync());
    }
    [Fact]
    public async Task ClosingWaitsForVehicleRegistrationSharedPeriodLock()
    {
        await LoginAsync("USER", "ADMIN");
        var id = await CreatePeriodAsync();
        await Client.PostAsync($"/api/v1/academic-periods/{id}/activate", null);
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        Assert.Equal(id, (await new AcademicPeriodRepository(context).GetActiveForShareAsync(default))!.Id);
        var closing = Client.PostAsync($"/api/v1/academic-periods/{id}/close", null);
        Assert.NotSame(closing, await Task.WhenAny(closing, Task.Delay(300)));
        await transaction.CommitAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await closing.WaitAsync(TimeSpan.FromSeconds(10))).StatusCode);
    }
    [Theory]
    [InlineData("period")]
    [InlineData("lot")]
    public async Task FailedAuditInsertRollsBackCompleteAggregate(string kind)
    {
        var admin = await LoginAsync("USER", "ADMIN");
        await using var factory = fixture.CreateFactory(services =>
        {
            services.RemoveAll<IAuditLogRepository>();
            services.AddScoped<IAuditLogRepository, InvalidAuditRepository>();
        });
        using var failing = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginExistingAsync(failing, admin);
        var response = kind == "period" ? await failing.PostAsJsonAsync("/api/v1/academic-periods", Period()) : await failing.PostAsJsonAsync("/api/v1/parking-lots", Lot());
        await ErrorAsync(response, HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.AcademicPeriods.ToListAsync());
        Assert.Empty(await context.ParkingLots.ToListAsync());
        Assert.Empty(await context.ParkingZones.ToListAsync());
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }
    [Fact]
    public async Task PaginationMissingResourcesAndPlannedClosureAreControlled()
    {
        await LoginAsync("USER", "ADMIN");
        var id = await CreatePeriodAsync();
        await ErrorAsync(await Client.PostAsync($"/api/v1/academic-periods/{id}/close", null), HttpStatusCode.Conflict, "ACADEMIC_PERIOD_NOT_ACTIVE");
        await ErrorAsync(await Client.PostAsync($"/api/v1/academic-periods/{Guid.NewGuid()}/activate", null), HttpStatusCode.NotFound, "ACADEMIC_PERIOD_NOT_FOUND");
        await ErrorAsync(await Client.PatchAsync($"/api/v1/parking-lots/{Guid.NewGuid()}/deactivate", null), HttpStatusCode.NotFound, "PARKING_LOT_NOT_FOUND");
        await CreateLotAsync();
        await CreateLotAsync("Otro");
        var page = (await Client.GetFromJsonAsync<PagedResponse<ParkingLotResponse>>("/api/v1/parking-lots?page=2&pageSize=1"))!;
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalCount);
        await ErrorAsync(await Client.GetAsync("/api/v1/parking-lots?pageSize=101"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Empty((await Client.GetFromJsonAsync<PagedResponse<ParkingLotResponse>>("/api/v1/parking-lots?page=2147483647&pageSize=100"))!.Items);
    }
    [Fact]
    public async Task RemovedAdminCannotManageWithOldTokenAndSwaggerDocumentsRoutes()
    {
        var admin = await LoginAsync("USER", "ADMIN");
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/v1/users/{admin.Id}/roles/ADMIN")).StatusCode);
        await ErrorAsync(await Client.PostAsJsonAsync("/api/v1/academic-periods", Period()), HttpStatusCode.Forbidden, "FORBIDDEN");
        using var document = JsonDocument.Parse(await Client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/api/v1/academic-periods", "/api/v1/academic-periods/current", "/api/v1/academic-periods/{id}/activate",
            "/api/v1/academic-periods/{id}/close", "/api/v1/parking-lots", "/api/v1/parking-lots/{id}", "/api/v1/parking-lots/{id}/activate", "/api/v1/parking-lots/{id}/deactivate" })
            Assert.True(paths.TryGetProperty(path, out _));
        Assert.True(paths.GetProperty("/api/v1/parking-lots").GetProperty("post").TryGetProperty("security", out _));
    }
    private sealed class InvalidAuditRepository(AppDbContext context) : IAuditLogRepository
    {
        public async Task AddAsync(AuditLog audit, CancellationToken cancellationToken) =>
            await context.AuditLogs.AddAsync(new AuditLog(Guid.NewGuid(), audit.Action, audit.EntityType, audit.EntityId, audit.CreatedAt), cancellationToken);
    }
}
