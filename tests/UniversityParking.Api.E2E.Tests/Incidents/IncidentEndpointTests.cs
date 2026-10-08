using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Api.E2E.Tests.Incidents;

[Collection("Authentication API")]
public sealed class IncidentEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "MotorBike-incidents-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;
    private User guard = null!, admin = null!, user = null!;
    private ParkingLot lot = null!;
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        guard = await fixture.CreateUserAsync("USER", "GUARD");
        admin = await fixture.CreateUserAsync("USER", "ADMIN");
        user = await fixture.CreateUserAsync("USER");
        factory = CreateFactory();
        client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        lot = new("Principal", "Kennedy", new(6, 0), new(22, 0), DateTimeOffset.UtcNow.AddDays(-1));
        await using var context = fixture.CreateContext();
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();
        await Login(guard);
    }
    private WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? extra = null) => fixture.CreateFactory(extra,
        new Dictionary<string, string?> { ["Storage:LocalRootPath"] = root, ["Storage:Provider"] = "Local" });
    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        var full = Path.GetFullPath(root);
        var prefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "MotorBike-incidents-");
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test directory.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
    private async Task Login(User actor, HttpClient? selected = null)
    {
        selected ??= client;
        var response = await selected.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(actor.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        selected.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }
    private MultipartFormDataContent Form(Dictionary<string, string>? extra = null, params (string Name, string File, string Mime, byte[] Bytes)[] files)
    {
        var fields = new Dictionary<string, string> { ["ParkingLotId"] = lot.Id.ToString(), ["Type"] = "SECURITY", ["Description"] = "Reporte de seguridad" };
        if (extra is not null) foreach (var item in extra) fields[item.Key] = item.Value;
        var form = new MultipartFormDataContent();
        foreach (var item in fields) form.Add(new StringContent(item.Value), item.Key);
        foreach (var file in files)
        {
            var content = new ByteArrayContent(file.Bytes);
            content.Headers.ContentType = new(file.Mime);
            form.Add(content, file.Name, file.File);
        }
        return form;
    }
    private async Task<Guid> Create(Dictionary<string, string>? fields = null, params (string, string, string, byte[])[] files)
    {
        using var form = Form(fields, files);
        var response = await client.PostAsync("/api/v1/incidents", form);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<IncidentCreatedResponse>())!.Id;
    }
    private async Task<IncidentDetailResponse> Detail(Guid id) => (await client.GetFromJsonAsync<IncidentDetailResponse>($"/api/v1/incidents/{id}"))!;
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GuardAndAdminCreate_WithTrustedActorAndDefaultUtc(bool asAdmin)
    {
        if (asAdmin) await Login(admin);
        var before = DateTimeOffset.UtcNow;
        var id = await Create();
        var detail = await Detail(id);
        Assert.Equal(asAdmin ? admin.Id : guard.Id, detail.Incident.ReportedBy);
        Assert.InRange(detail.Incident.OccurredAt, before, DateTimeOffset.UtcNow);
        Assert.Equal("OPEN", detail.Incident.Status);
        Assert.Null(detail.Incident.UserId);
        Assert.Empty(detail.Attachments);
        await using var context = fixture.CreateContext();
        var audit = await context.AuditLogs.SingleAsync(x => x.EntityId == id);
        Assert.Equal("INCIDENT_CREATED", audit.Action);
    }
    [Fact]
    public async Task AdminResolves_RecordsActorAndUtc_ThenRejectsFurtherTransitions()
    {
        var id = await Create();
        await Login(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new ResolveIncidentRequest("Verificado"))).StatusCode);
        var detail = await Detail(id);
        Assert.Equal("RESOLVED", detail.Incident.Status);
        Assert.Equal(admin.Id, detail.Incident.ResolvedBy);
        Assert.NotNull(detail.Incident.ResolvedAt);
        Assert.Equal("Verificado", detail.Incident.Resolution);
        await Error(await client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new ResolveIncidentRequest("Otro")), HttpStatusCode.Conflict, "INCIDENT_ALREADY_RESOLVED");
        await Error(await client.PostAsync($"/api/v1/incidents/{id}/cancel", null), HttpStatusCode.Conflict, "INCIDENT_NOT_OPEN");
    }
    [Fact]
    public async Task CancellationIsIdempotentAndKeepsEvidence()
    {
        var id = await Create(null, ("Attachments[0]", "evidence.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7 evidence")));
        var attachment = Assert.Single((await Detail(id)).Attachments);
        await Login(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/incidents/{id}/cancel", new CancelIncidentRequest("Duplicado"))).StatusCode);
        var first = (await Detail(id)).Incident;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/incidents/{id}/cancel", null)).StatusCode);
        Assert.Equal(first, (await Detail(id)).Incident);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(attachment.ContentUrl)).StatusCode);
        await Error(await client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new ResolveIncidentRequest("No")), HttpStatusCode.Conflict, "INCIDENT_NOT_OPEN");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "INCIDENT_CANCELLED"));
    }
    [Fact]
    public async Task NormalUserCannotOperateOrReadAttachments_AnonymousCannotRead()
    {
        var id = await Create(null, ("Attachments[0]", "evidence.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7 evidence")));
        var url = Assert.Single((await Detail(id)).Attachments).ContentUrl;
        await Login(user);
        foreach (var path in new[] { "/api/v1/incidents", $"/api/v1/incidents/{id}", url })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        using var form = Form();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/incidents", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/incidents/{id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new ResolveIncidentRequest("No"))).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }
    [Fact]
    public async Task GuardCannotResolveOrCancel()
    {
        var id = await Create();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/incidents/{id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new ResolveIncidentRequest("No"))).StatusCode);
    }
    [Theory]
    [InlineData("ReportedBy")] [InlineData("StorageKey")] [InlineData("Status")]
    public async Task RejectsInjectedFields(string field)
    {
        using var form = Form(new() { [field] = Guid.NewGuid().ToString() });
        await Error(await client.PostAsync("/api/v1/incidents", form), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Theory]
    [InlineData("Attachments[1]")] [InlineData("Attachments[00]")] [InlineData("Attachments[0].StorageKey")]
    public async Task RejectsMalformedAttachmentIndexes(string name)
    {
        using var form = Form(null, (name, "e.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7")));
        await Error(await client.PostAsync("/api/v1/incidents", form), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [Theory]
    [InlineData("UserId", "USER_NOT_FOUND")] [InlineData("VehicleId", "VEHICLE_NOT_FOUND")]
    [InlineData("ParkingMovementId", "PARKING_MOVEMENT_NOT_FOUND")] [InlineData("ParkingLotId", "PARKING_LOT_NOT_FOUND")]
    public async Task RejectsMissingReferences(string field, string code)
    {
        using var form = Form(new() { [field] = Guid.NewGuid().ToString() });
        await Error(await client.PostAsync("/api/v1/incidents", form), HttpStatusCode.NotFound, code);
    }
    [Theory]
    [InlineData("UserId")] [InlineData("VehicleId")] [InlineData("ParkingLotId")]
    public async Task RejectsContradictoryMovementReferences(string mismatch)
    {
        await using var context = fixture.CreateContext();
        var vehicle = new Vehicle(VehicleType.MOTORCYCLE, new VehiclePlate("ABC123"), null, "Brand", "Model", "Black", DateTimeOffset.UtcNow);
        var other = new ParkingLot("Otro", "Centro", new(6, 0), new(22, 0), DateTimeOffset.UtcNow);
        var zone = new ParkingZone(lot.Id, "Motos", VehicleType.MOTORCYCLE, DateTimeOffset.UtcNow);
        var movement = new ParkingMovement(user.Id, vehicle.Id, lot.Id, zone.Id, DateTimeOffset.UtcNow, guard.Id);
        context.AddRange(vehicle, other, zone, movement);
        await context.SaveChangesAsync();
        var fields = new Dictionary<string, string> { ["ParkingMovementId"] = movement.Id.ToString() };
        if (mismatch == "UserId") fields[mismatch] = admin.Id.ToString();
        if (mismatch == "ParkingLotId") fields[mismatch] = other.Id.ToString();
        if (mismatch == "VehicleId")
        {
            var second = new Vehicle(VehicleType.MOTORCYCLE, new VehiclePlate("XYZ123"), null, "Brand", "Model", "Black", DateTimeOffset.UtcNow);
            context.Vehicles.Add(second); await context.SaveChangesAsync(); fields[mismatch] = second.Id.ToString();
        }
        using var form = Form(fields);
        await Error(await client.PostAsync("/api/v1/incidents", form), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        fields["ParkingLotId"] = lot.Id.ToString(); fields["UserId"] = user.Id.ToString(); fields["VehicleId"] = vehicle.Id.ToString();
        var id = await Create(fields);
        Assert.Equal(movement.Id, (await Detail(id)).Incident.ParkingMovementId);
    }
    [Fact]
    public async Task AttachmentContentIsPrivateAndBoundToIncident_StorageKeysNeverExposed()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7 evidence");
        var id = await Create(null, ("Attachments[0]", "evidence.pdf", "application/pdf", bytes));
        var attachment = Assert.Single((await Detail(id)).Attachments);
        var response = await client.GetAsync(attachment.ContentUrl);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.True(response.Headers.CacheControl.NoStore);
        var other = await Create();
        await Error(await client.GetAsync($"/api/v1/incidents/{other}/attachments/{attachment.Id}/content"), HttpStatusCode.NotFound, "INCIDENT_NOT_FOUND");
        Assert.DoesNotContain("storageKey", await (await client.GetAsync($"/api/v1/incidents/{id}")).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await using var context = fixture.CreateContext();
        var key = (await context.IncidentAttachments.SingleAsync()).StorageKey;
        Assert.StartsWith($"incidents/{id:D}/attachments/", key);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/" + key)).StatusCode);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RejectsInvalidOrOversizedFileBeforeAnyUpload(bool oversize)
    {
        var bytes = oversize ? new byte[10 * 1024 * 1024 + 1] : Encoding.ASCII.GetBytes("invalid pdf");
        using var form = Form(null, ("Attachments[0]", "e.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7")),
            ("Attachments[1]", "bad.pdf", "application/pdf", bytes));
        await Error(await client.PostAsync("/api/v1/incidents", form), HttpStatusCode.BadRequest, oversize ? "FILE_TOO_LARGE" : "FILE_TYPE_NOT_ALLOWED");
        Assert.False(Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any());
        await using var context = fixture.CreateContext();
        Assert.False(await context.Incidents.AnyAsync());
    }
    [Fact]
    public async Task DatabaseFailureRollsBackIncidentAuditAndNewFiles_PreservesHistoricalEvidence()
    {
        var historical = await Create(null, ("Attachments[0]", "e.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7")));
        await using var failing = CreateFactory(services => services.AddScoped<IAuditLogRepository, FailingAudit>());
        using var selected = failing.CreateClient(new() { BaseAddress = new("https://localhost") });
        await Login(guard, selected);
        using var form = Form(null, ("Attachments[0]", "new.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7")));
        Assert.Equal(HttpStatusCode.InternalServerError, (await selected.PostAsync("/api/v1/incidents", form)).StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(historical, (await context.Incidents.SingleAsync()).Id);
        Assert.Single(await context.IncidentAttachments.ToListAsync());
        Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task ListsWithAllFiltersInclusiveBogotaDaysAndPagination()
    {
        var fields = new Dictionary<string, string> { ["UserId"] = user.Id.ToString(), ["OccurredAt"] = "2026-10-07T23:59:59-05:00" };
        var id = await Create(fields);
        await Create(new() { ["OccurredAt"] = "2026-10-08T00:00:00-05:00" });
        await Create(new() { ["OccurredAt"] = "2026-10-06T23:59:59-05:00" });
        var path = $"/api/v1/incidents?status=OPEN&type=SECURITY&parkingLotId={lot.Id}&userId={user.Id}&dateFrom=2026-10-07&dateTo=2026-10-07&pageSize=1";
        var page = (await client.GetFromJsonAsync<PagedResponse<IncidentResponse>>(path))!;
        Assert.Equal(1, page.TotalCount); Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.Equal(TimeSpan.Zero, (await Detail(id)).Incident.OccurredAt.Offset);
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<IncidentResponse>>(path + "&page=2147483647"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<IncidentResponse>>(path + "&vehicleId=" + Guid.NewGuid()))!.Items);
    }
    [Theory]
    [InlineData("page=0")] [InlineData("pageSize=101")] [InlineData("status=999")]
    [InlineData("dateFrom=2026-10-08&dateTo=2026-10-07")] [InlineData("userId=00000000-0000-0000-0000-000000000000")]
    public async Task InvalidListInputsReturnValidation(string query) =>
        await Error(await client.GetAsync("/api/v1/incidents?" + query), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    [Fact]
    public async Task RejectsBlankResolutionUnknownJsonAndMissingIncident()
    {
        var id = await Create(); await Login(admin);
        await Error(await client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new ResolveIncidentRequest("  ")), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new { resolution = "ok", resolvedBy = guard.Id }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.GetAsync($"/api/v1/incidents/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "INCIDENT_NOT_FOUND");
        await Error(await client.PostAsync($"/api/v1/incidents/{Guid.NewGuid()}/cancel", null), HttpStatusCode.NotFound, "INCIDENT_NOT_FOUND");
    }
    [Fact]
    public async Task ConcurrentResolveCancelProducesOneTransitionAndOneAudit()
    {
        var id = await Create(); await Login(admin);
        var secondAdmin = await fixture.CreateUserAsync("USER", "ADMIN");
        using var secondClient = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        await Login(secondAdmin, secondClient);
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/v1/incidents/{id}/resolve", new ResolveIncidentRequest("Revisado")),
            secondClient.PostAsync($"/api/v1/incidents/{id}/cancel", null));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.EntityId == id && x.Action != "INCIDENT_CREATED"));
    }
    [Fact]
    public async Task StaleGuardRoleIsRejectedInApplication()
    {
        await using var context = fixture.CreateContext();
        var role = await context.Roles.SingleAsync(x => x.Code == "GUARD");
        context.UserRoles.Remove(await context.UserRoles.SingleAsync(x => x.UserId == guard.Id && x.RoleId == role.Id));
        await context.SaveChangesAsync();
        await Error(await client.GetAsync("/api/v1/incidents"), HttpStatusCode.Forbidden, "FORBIDDEN");
        using var form = Form();
        await Error(await client.PostAsync("/api/v1/incidents", form), HttpStatusCode.Forbidden, "FORBIDDEN");
    }
    [Fact]
    public async Task OpenApiDescribesIndexedMultipartAndPrivateBinaryContent()
    {
        using var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = swagger.RootElement.GetProperty("paths");
        var create = paths.GetProperty("/api/v1/incidents").GetProperty("post");
        Assert.Contains("Attachments[0]", create.GetProperty("description").GetString());
        Assert.True(create.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        var content = paths.GetProperty("/api/v1/incidents/{incidentId}/attachments/{attachmentId}/content").GetProperty("get").GetProperty("responses");
        Assert.True(content.GetProperty("200").GetProperty("content").TryGetProperty("application/pdf", out _));
        Assert.True(content.TryGetProperty("302", out _));
    }
    private sealed class FailingAudit(UniversityParking.Infrastructure.Persistence.AppDbContext context) : IAuditLogRepository
    {
        public async Task AddAsync(AuditLog audit, CancellationToken cancellationToken) =>
            await context.AuditLogs.AddAsync(new(Guid.NewGuid(), audit.Action, audit.EntityType, audit.EntityId, audit.CreatedAt), cancellationToken);
    }
}
