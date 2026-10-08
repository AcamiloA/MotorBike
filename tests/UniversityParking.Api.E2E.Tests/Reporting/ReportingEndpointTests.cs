using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.News;
using UniversityParking.Contracts.Reporting;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Infrastructure.Authentication;
using UniversityParking.Infrastructure.Time;

namespace UniversityParking.Api.E2E.Tests.Reporting;

[Collection("Authentication API")]
public sealed class ReportingEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;
    private User admin = null!, guard = null!, target = null!;
    private Vehicle vehicle = null!;
    private ParkingLot lot = null!, otherLot = null!;
    private AcademicPeriod period = null!;
    private readonly DateTimeOffset start = DateTimeOffset.Parse("2026-10-06T05:00:00Z");
    private const string Range = "dateFrom=2026-10-06&dateTo=2026-10-06";
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        admin = await fixture.CreateUserAsync("USER", "ADMIN"); guard = await fixture.CreateUserAsync("USER", "GUARD");
        factory = fixture.CreateFactory(services =>
        {
            services.AddSingleton<IClock>(new Clock());
            services.AddScoped<ITokenService>(p => new JwtTokenService(p.GetRequiredService<IOptions<JwtOptions>>(), new SystemClock()));
        });
        client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        target = new(new IdentificationNumber("STUDENT123"), "Estudiante", UniversityParking.Domain.Universities.UniversityIds.Etitc, "Ingeniería", MemberType.STUDENT, new CardCode("CARDSTUDENT"), start.AddDays(-5));
        lot = new("Principal", "Kennedy", new(6,0), new(22,0), start.AddDays(-5));
        otherLot = new("Secundario", "Centro", new(6,0), new(22,0), start.AddDays(-5));
        var zone = new ParkingZone(lot.Id, "Motos", VehicleType.MOTORCYCLE, start.AddDays(-5));
        var otherZone = new ParkingZone(otherLot.Id, "Motos", VehicleType.MOTORCYCLE, start.AddDays(-5));
        vehicle = new(VehicleType.MOTORCYCLE, new VehiclePlate("ABC123"), null, "Marca", "Modelo", "Negro", start.AddDays(-5));
        period = new("2026-2", new(2026,7,1), new(2026,12,31), start.AddDays(-5)); period.Activate();
        var ownership = new VehicleOwnership(vehicle.Id, target.Id, start.AddDays(-5), admin.Id); ownership.Close(start.AddDays(-1), "Transferencia");
        var current = new VehicleOwnership(vehicle.Id, admin.Id, start.AddDays(-1), admin.Id);
        var movement = new ParkingMovement(target.Id, vehicle.Id, lot.Id, zone.Id, start, guard.Id); movement.Close(start.AddHours(1), guard.Id);
        var old = new ParkingMovement(target.Id, vehicle.Id, lot.Id, zone.Id, start.AddHours(-1), guard.Id); old.Close(start.AddHours(2), guard.Id);
        var future = new ParkingMovement(target.Id, vehicle.Id, lot.Id, zone.Id, start.AddDays(1), guard.Id); future.Close(start.AddDays(1).AddHours(1), guard.Id);
        var inside = new ParkingMovement(target.Id, vehicle.Id, otherLot.Id, otherZone.Id, start.AddHours(3), guard.Id);
        var incident = new Incident(lot.Id, guard.Id, IncidentType.SECURITY, "Reporte", start, start, parkingMovementId: movement.Id);
        var closedIncident = new Incident(lot.Id, guard.Id, IncidentType.OTHER, "Resuelto", start, start); closedIncident.Resolve("Revisado", admin.Id, start.AddHours(1));
        await using var context = fixture.CreateContext();
        context.AddRange(target, lot, otherLot, zone, otherZone, vehicle, period, ownership, current,
            new VehicleRegistration(vehicle.Id, target.Id, period.Id, start.AddDays(-2)), movement, old, future, inside, incident, closedIncident);
        context.AuditLogs.AddRange(new AuditLog(admin.Id, "USER_UPDATED", "User", target.Id, start, "{\"fullName\":\"Antes\"}", "{\"fullName\":\"Después\"}", "127.0.0.1", "audit-trace"),
            new AuditLog(guard.Id, "PARKING_CHECK_IN", "ParkingMovement", movement.Id, start.AddHours(-1)),
            new AuditLog(admin.Id, "USER_UPDATED", "User", target.Id, start.AddDays(1)));
        await context.SaveChangesAsync(); await Login(admin);
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task Login(User actor)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(actor.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }
    private async Task<T> Read<T>(string path)
    {
        var response = await client.GetAsync(path); Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString()); Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }
    [Fact]
    public async Task ReferenceChangeDoesNotAlterAccessCountsOrHistory()
    {
        var daily = await Read<DailyAccessResponse[]>("/api/v1/reports/access/daily?" + Range);
        var groups = await Read<AccessGroupResponse[]>("/api/v1/reports/access/by-member-type?" + Range);
        var history = await Read<VehicleHistoryResponse>($"/api/v1/reports/vehicles/{vehicle.Id}/history?pageSize=20");
        await using (var db = fixture.CreateContext())
        {
            var user = await db.Users.SingleAsync(x => x.Id == target.Id);
            user.Update(user.FullName, new Guid("a1100000-0000-4000-8000-000000000002"),
                user.Career, user.MemberType, user.CardCode, start.AddDays(2));
            await db.SaveChangesAsync();
        }
        Assert.Equal(daily, await Read<DailyAccessResponse[]>("/api/v1/reports/access/daily?" + Range));
        Assert.Equal(groups, await Read<AccessGroupResponse[]>("/api/v1/reports/access/by-member-type?" + Range));
        var after = await Read<VehicleHistoryResponse>($"/api/v1/reports/vehicles/{vehicle.Id}/history?pageSize=20");
        Assert.Equal(JsonSerializer.Serialize(history), JsonSerializer.Serialize(after));
    }
    [Fact]
    public async Task AdminDashboardUsesLocalTodayAndRealCounts()
    {
        var value = await Read<AdminDashboardResponse>("/api/v1/dashboard/admin");
        Assert.Equal(3, value.ActiveUsers); Assert.Equal(1, value.ActiveVehicles); Assert.Equal(1, value.VehiclesInside);
        Assert.Equal(2, value.TodayCheckIns); Assert.Equal(2, value.TodayCheckOuts); Assert.Equal(1, value.OpenIncidents);
        Assert.Equal(period.Id, value.CurrentAcademicPeriod!.Id);
        await using var context = fixture.CreateContext(); (await context.AcademicPeriods.SingleAsync()).Close(); await context.SaveChangesAsync();
        Assert.Null((await Read<AdminDashboardResponse>("/api/v1/dashboard/admin")).CurrentAcademicPeriod);
    }
    [Fact]
    public async Task GuardDashboardIsScopedToRequestedLotAndAdminDoesNotImplyGuard()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/dashboard/guard?parkingLotId={lot.Id}")).StatusCode);
        await Login(guard);
        var value = await Read<GuardDashboardResponse>($"/api/v1/dashboard/guard?parkingLotId={lot.Id}");
        Assert.Equal(lot.Id, value.ParkingLot.Id); Assert.Equal(0, value.VehiclesInside); Assert.Equal(1, value.TodayCheckIns);
        Assert.Equal(2, value.TodayCheckOuts); Assert.Equal(1, value.OpenIncidents);
        var other = await Read<GuardDashboardResponse>($"/api/v1/dashboard/guard?parkingLotId={otherLot.Id}");
        Assert.Equal(1, other.VehiclesInside); Assert.Equal(1, other.TodayCheckIns); Assert.Equal(0, other.OpenIncidents);
        await Error(await client.GetAsync("/api/v1/dashboard/guard"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.GetAsync($"/api/v1/dashboard/guard?parkingLotId={Guid.NewGuid()}"), HttpStatusCode.NotFound, "PARKING_LOT_NOT_FOUND");
    }
    [Fact]
    public async Task DailyCountsCheckOutWhoseCheckInWasOutsideRange()
    {
        var all = Assert.Single(await Read<DailyAccessResponse[]>("/api/v1/reports/access/daily?" + Range));
        Assert.Equal(new DateOnly(2026,10,6), all.Date); Assert.Equal(2, all.CheckIns); Assert.Equal(2, all.CheckOuts);
        var filtered = Assert.Single(await Read<DailyAccessResponse[]>($"/api/v1/reports/access/daily?{Range}&parkingLotId={lot.Id}"));
        Assert.Equal(1, filtered.CheckIns); Assert.Equal(2, filtered.CheckOuts);
        Assert.Empty(await Read<DailyAccessResponse[]>($"/api/v1/reports/access/daily?{Range}&parkingLotId={Guid.NewGuid()}"));
    }
    [Fact]
    public async Task VehicleAndMemberGroupsUseMovementUserRatherThanCurrentOwner()
    {
        var vehicleGroup = Assert.Single(await Read<AccessGroupResponse[]>("/api/v1/reports/access/by-vehicle-type?" + Range));
        Assert.Equal("MOTORCYCLE", vehicleGroup.Type); Assert.Equal(2, vehicleGroup.CheckIns); Assert.Equal(2, vehicleGroup.CheckOuts);
        var memberGroup = Assert.Single(await Read<AccessGroupResponse[]>("/api/v1/reports/access/by-member-type?" + Range));
        Assert.Equal("STUDENT", memberGroup.Type); Assert.Equal(2, memberGroup.CheckIns); Assert.Equal(2, memberGroup.CheckOuts);
    }
    [Fact]
    public async Task VehicleAndUserHistoriesKeepPastOwnershipAndIndirectIncidents()
    {
        var history = await Read<VehicleHistoryResponse>($"/api/v1/reports/vehicles/{vehicle.Id}/history?pageSize=1");
        Assert.Equal(2, history.Ownerships.TotalCount); Assert.Equal(admin.Id, Assert.Single(history.Ownerships.Items).UserId);
        Assert.Equal(1, history.Registrations.TotalCount); Assert.Equal(4, history.Movements.TotalCount); Assert.Equal(1, history.Incidents.TotalCount);
        var user = await Read<UserHistoryResponse>($"/api/v1/reports/users/{target.Id}/history");
        Assert.NotNull(Assert.Single(user.Ownerships.Items).EndAt); Assert.Equal(4, user.Movements.TotalCount); Assert.Equal(1, user.Incidents.TotalCount);
        var past = await Read<VehicleHistoryResponse>($"/api/v1/reports/vehicles/{vehicle.Id}/history?page=2&pageSize=1");
        Assert.Equal(target.Id, Assert.Single(past.Ownerships.Items).UserId);
        var empty = await Read<VehicleHistoryResponse>($"/api/v1/reports/vehicles/{vehicle.Id}/history?page=2147483647");
        Assert.Empty(empty.Ownerships.Items); Assert.Empty(empty.Movements.Items);
        var raw = await (await client.GetAsync($"/api/v1/reports/users/{target.Id}/history")).Content.ReadAsStringAsync();
        foreach (var secret in new[] { "password", "passwordHash", "storageKey", "accessToken", "documentContent" }) Assert.DoesNotContain(secret, raw, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task GuardActivityCountsReportingTimeAndSeparateEntryExitDates()
    {
        var value = await Read<GuardActivityResponse>($"/api/v1/reports/guards/{guard.Id}/activity?{Range}");
        Assert.Equal(2, value.CheckIns); Assert.Equal(2, value.CheckOuts); Assert.Equal(2, value.IncidentsReported);
        var inactive = await Read<GuardActivityResponse>($"/api/v1/reports/guards/{admin.Id}/activity?{Range}");
        Assert.Equal(0, inactive.CheckIns);
    }
    [Fact]
    public async Task AuditFiltersPaginationAndDateBoundaries()
    {
        var path = $"/api/v1/audit-logs?actorUserId={admin.Id}&action=USER_UPDATED&entityType=User&entityId={target.Id}&{Range}";
        var value = await Read<PagedResponse<AuditResponse>>(path); var audit = Assert.Single(value.Items);
        Assert.Equal(1, value.TotalCount); Assert.Equal("audit-trace", audit.TraceId); Assert.Equal(start, audit.CreatedAt);
        Assert.Contains("fullName", audit.NewValues); Assert.Equal("127.0.0.1", audit.IpAddress);
        Assert.Empty((await Read<PagedResponse<AuditResponse>>(path + "&page=2147483647")).Items);
        Assert.Equal(3, (await Read<PagedResponse<AuditResponse>>("/api/v1/audit-logs?pageSize=1")).TotalCount);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.SendAsync(new(method, "/api/v1/audit-logs"))).StatusCode);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UserAndGuardCannotReadAdministrativeQueries(bool isGuard)
    {
        var normal = await fixture.CreateUserAsync("USER"); await Login(isGuard ? guard : normal);
        foreach (var path in new[] { "/api/v1/audit-logs", "/api/v1/dashboard/admin", "/api/v1/reports/access/daily?" + Range,
            "/api/v1/reports/access/by-vehicle-type?" + Range, "/api/v1/reports/access/by-member-type?" + Range,
            $"/api/v1/reports/vehicles/{vehicle.Id}/history", $"/api/v1/reports/users/{target.Id}/history", $"/api/v1/reports/guards/{guard.Id}/activity?{Range}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    [Theory]
    [InlineData("/api/v1/reports/access/daily")] [InlineData("/api/v1/reports/access/by-vehicle-type")]
    [InlineData("/api/v1/reports/access/by-member-type")]
    public async Task ReportsRejectMissingOrReversedDates(string path)
    {
        await Error(await client.GetAsync(path), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.GetAsync(path + "?dateFrom=2026-10-07&dateTo=2026-10-06"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Theory]
    [InlineData("page=0")] [InlineData("pageSize=101")]
    [InlineData("dateFrom=2026-10-07&dateTo=2026-10-06")]
    public async Task AuditRejectsInvalidFilters(string query) => await Error(await client.GetAsync("/api/v1/audit-logs?" + query), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    [Fact]
    public async Task MissingHistorySubjectsReturnNotFound()
    {
        await Error(await client.GetAsync($"/api/v1/reports/vehicles/{Guid.NewGuid()}/history"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Error(await client.GetAsync($"/api/v1/reports/users/{Guid.NewGuid()}/history"), HttpStatusCode.NotFound, "USER_NOT_FOUND");
        await Error(await client.GetAsync($"/api/v1/reports/guards/{Guid.NewGuid()}/activity?{Range}"), HttpStatusCode.NotFound, "USER_NOT_FOUND");
    }
    [Fact]
    public async Task RealNewsAuditContainsNoSecretsAndHasTrustedActor()
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/news", new CreateNewsRequest("Aviso", "Contenido público")); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<NewsCreatedResponse>())!.Id;
        var audit = Assert.Single((await Read<PagedResponse<AuditResponse>>($"/api/v1/audit-logs?entityId={id}")).Items);
        Assert.Equal(admin.Id, audit.ActorUserId); Assert.Equal("NEWS_CREATED", audit.Action); Assert.NotNull(audit.TraceId);
        var raw = JsonSerializer.Serialize(audit);
        foreach (var secret in new[] { AuthApiFixture.Password, "passwordHash", "AccessToken", "Authorization", "ConnectionStrings", "secretKey" })
            Assert.DoesNotContain(secret, raw, StringComparison.OrdinalIgnoreCase);
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-10-07T02:00:00Z"); }
}
