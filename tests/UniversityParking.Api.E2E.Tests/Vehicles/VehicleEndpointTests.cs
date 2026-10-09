using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Parking;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.E2E.Tests.Vehicles;

[Collection("Authentication API")]
public sealed partial class VehicleEndpointTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "UniversityParking-vehicles-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;
    private static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aG6kAAAAASUVORK5CYII=");
    private static byte[] Pdf => "%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateContext();
        context.Roles.AddRange(new Role("USER"), new Role("GUARD"), new Role("ADMIN"));
        var period = new AcademicPeriod("2026-2", new(2026, 7, 1), new(2026, 12, 31), DateTimeOffset.UtcNow);
        period.Activate();
        context.AcademicPeriods.Add(period);
        await context.SaveChangesAsync();
        factory = CreateFactory();
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
    }
    private WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? services = null, bool? ocrEnabled = null) => fixture.CreateFactory(services,
        new Dictionary<string, string?> { ["Storage:Provider"] = "Local", ["Storage:LocalRootPath"] = root, ["DocumentOcr:Enabled"] = (ocrEnabled ?? true).ToString() });
    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        var full = Path.GetFullPath(root);
        if (!full.StartsWith(Path.Combine(Path.GetTempPath(), "UniversityParking-vehicles-"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected test storage directory.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
    private async Task<User> CreateUserAsync(MemberType member = MemberType.STUDENT, params string[] roles)
    {
        await using var context = fixture.CreateContext();
        var user = new User(new IdentificationNumber(Guid.NewGuid().ToString("N")), "Usuario de vehículos", UniversityParking.Domain.Universities.UniversityIds.Etitc,
            member == MemberType.STUDENT ? "Ingeniería" : null, member, new CardCode(Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow);
        context.Users.Add(user);
        context.UserCredentials.Add(new UserCredential(user.Id, factory.Services.GetRequiredService<IPasswordHasher>().Hash(AuthApiFixture.Password), user.CreatedAt));
        foreach (var code in roles.Append("USER").Distinct())
            context.UserRoles.Add(new UserRole(user.Id, (await context.Roles.SingleAsync(x => x.Code == code)).Id));
        await context.SaveChangesAsync();
        return user;
    }
    private async Task LoginAsync(User user, HttpClient? target = null)
    {
        target ??= client;
        var response = await target.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.IdentificationNumber.Value, AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        target.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
    }
    private static MultipartFormDataContent Form(string type = "MOTORCYCLE", string identifier = "ABC123", bool photo = true,
        string? documentTypes = null, byte[]? photoBytes = null, string photoName = "original.png", string photoMime = "image/png")
    {
        var body = new MultipartFormDataContent();
        body.Add(new StringContent(type), "Type");
        body.Add(new StringContent(identifier), type is "BICYCLE" or "SCOOTER" ? "FrameNumber" : "Plate");
        body.Add(new StringContent("Brand"), "Brand");
        body.Add(new StringContent("Model"), "Model");
        body.Add(new StringContent("Black"), "Color");
        if (photo)
        {

            var file = new ByteArrayContent(photoBytes ?? Png);
            file.Headers.ContentType = new MediaTypeHeaderValue(photoMime);
            body.Add(file, "VerificationImage.File", photoName);
        }
        var types = (documentTypes ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < types.Length; i++)
        {
            body.Add(new StringContent(types[i]), $"Documents[{i}].Type");
            var file = new ByteArrayContent(Pdf);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            body.Add(file, $"Documents[{i}].File", "support.pdf");
        }
        return body;
    }
    private async Task<Guid> RegisterAsync(string type = "MOTORCYCLE", string identifier = "ABC123")
    {
        using var body = Form(type, identifier);
        var response = await client.PostAsync("/api/v1/vehicles", body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<VehicleCreatedResponse>())!.Id;
        Assert.EndsWith(id.ToString(), response.Headers.Location!.ToString());
        return id;
    }
    private static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }
    private static ByteArrayContent EmptyRenewal()
    {
        var content = new ByteArrayContent([]);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=NoNewDocuments");
        return content;
    }
    private int FileCount() => Directory.Exists(root) ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count() : 0;
    [Theory]
    [InlineData(MemberType.STUDENT, "MOTORCYCLE")]
    [InlineData(MemberType.STUDENT, "BICYCLE")]
    [InlineData(MemberType.TEACHER, "CAR")]
    [InlineData(MemberType.STAFF, "CAR")]
    public async Task RegistrationCreatesVehicleOwnerPeriodEvidenceAndAudit(MemberType member, string type)
    {
        var user = await CreateUserAsync(member);
        await LoginAsync(user);
        var id = await RegisterAsync(type, type == "BICYCLE" ? " FRAME 0001 " : " abc-123 ");
        await using var context = fixture.CreateContext();
        Assert.Equal(user.Id, (await context.VehicleOwnerships.SingleAsync()).UserId);
        Assert.Equal(user.Id, (await context.VehicleRegistrations.SingleAsync()).UserId);
        Assert.Equal(1, await context.VehicleVerificationImages.CountAsync()); Assert.Equal(0, await context.VehiclePhotos.CountAsync());
        Assert.Equal(0, await context.VehicleDocuments.CountAsync());
        Assert.Equal(1, FileCount());
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "VEHICLE_REGISTERED"));
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "VEHICLE_REGISTRATION_CREATED"));
        var detail = (await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!;
        Assert.Equal(type, detail.Vehicle.Type);
        Assert.Equal(type == "BICYCLE" ? "BICYCLE_PHOTO" : "TRANSIT_LICENSE_FRONT", detail.VerificationImage!.Type);
        Assert.Equal("image/png", detail.VerificationImage.ContentType); Assert.Empty(detail.Photos); Assert.Empty(detail.Documents);
        Assert.Equal("ACTIVE", detail.Vehicle.RegistrationState);
        Assert.False(detail.Vehicle.IsInside);
        Assert.Equal(type == "BICYCLE" ? "FRAME0001" : "ABC123", detail.Vehicle.Plate ?? detail.Vehicle.FrameNumber);
        var mine = (await client.GetFromJsonAsync<VehicleResponse[]>("/api/v1/vehicles/me"))!;
        Assert.Equal(id, Assert.Single(mine).Id);
        Assert.DoesNotContain("storageKey", await client.GetStringAsync($"/api/v1/vehicles/{id}"), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task StudentCarRejectedWithoutDatabaseOrStorageWrites()
    {
        await LoginAsync(await CreateUserAsync());
        using var body = Form("CAR");
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", body), HttpStatusCode.Conflict, "STUDENT_CANNOT_REGISTER_CAR");
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.Vehicles.ToListAsync());
        Assert.Empty(await context.VehicleOwnerships.ToListAsync());
        Assert.Empty(await context.VehicleRegistrations.ToListAsync());
        Assert.Equal(0, FileCount());
    }
    [Fact]
    public async Task MissingActivePeriodIsRejectedBeforeUpload()
    {
        await using (var context = fixture.CreateContext())
        {
            (await context.AcademicPeriods.SingleAsync()).Close();
            await context.SaveChangesAsync();
        }
        await LoginAsync(await CreateUserAsync());
        using var body = Form();
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", body), HttpStatusCode.Conflict, "ACADEMIC_PERIOD_NOT_ACTIVE");
        Assert.Equal(0, FileCount());
    }
    [Theory]
    [InlineData(false, "VEHICLE_REGISTRATION,INSURANCE")]
    [InlineData(true, "VEHICLE_REGISTRATION")]
    public async Task MandatoryEvidenceIsRequired(bool photo, string documents)
    {
        await LoginAsync(await CreateUserAsync());
        using var body = Form(photo: photo, documentTypes: documents);
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", body), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(0, FileCount());
    }
    [Theory]
    [InlineData("script.png", "image/png", "magic")]
    [InlineData("script.exe", "image/png", "extension")]
    [InlineData("photo.png", "application/pdf", "mime")]
    public async Task InvalidFilesAreRejectedBeforeAnyStorageWrite(string name, string mime, string mode)
    {
        await LoginAsync(await CreateUserAsync());
        using var body = Form(photoBytes: mode == "magic" ? "<script>bad</script>"u8.ToArray() : Png, photoName: name, photoMime: mime);
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", body), HttpStatusCode.BadRequest, "FILE_TYPE_NOT_ALLOWED");
        Assert.Equal(0, FileCount());
    }
    [Fact]
    public async Task OversizePhotoIsRejected()
    {
        await LoginAsync(await CreateUserAsync());
        using var body = Form(photoBytes: new byte[5 * 1024 * 1024 + 1]);
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", body), HttpStatusCode.BadRequest, "FILE_TOO_LARGE");
        Assert.Equal(0, FileCount());
    }
    [Fact]
    public async Task OtherUserCannotReadEditRenewOrDownloadVehicle()
    {
        await LoginAsync(await CreateUserAsync());
        var id = await RegisterAsync();
        await AddLegacyAsync(id);
        var detail = (await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!;
        await LoginAsync(await CreateUserAsync());
        await ErrorAsync(await client.GetAsync($"/api/v1/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await ErrorAsync(await client.PutAsJsonAsync($"/api/v1/vehicles/{id}", new UpdateVehicleRequest("New", "New", "New")), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await ErrorAsync(await client.GetAsync(detail.VerificationImage!.ContentUrl), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await ErrorAsync(await client.GetAsync(detail.Documents[0].ContentUrl), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        using var renewal = EmptyRenewal();
        await ErrorAsync(await client.PostAsync($"/api/v1/vehicles/{id}/renew", renewal), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
    }
    [Fact]
    public async Task ContentIsPrivate_PhotosAllowGuard_DocumentsDoNot()
    {
        await LoginAsync(await CreateUserAsync());
        var id = await RegisterAsync();
        await AddLegacyAsync(id);
        var detail = (await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!;
        var photo = await client.GetAsync(detail.VerificationImage!.ContentUrl);
        Assert.Equal(Png, await photo.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", Assert.Single(photo.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(photo.Headers.CacheControl!.NoStore);
        await LoginAsync(await CreateUserAsync(MemberType.STAFF, "GUARD"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(detail.VerificationImage!.ContentUrl)).StatusCode);
        await ErrorAsync(await client.GetAsync(detail.Documents[0].ContentUrl), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await LoginAsync(await CreateUserAsync(MemberType.STAFF, "ADMIN"));
        Assert.Equal(Pdf, await (await client.GetAsync(detail.Documents[0].ContentUrl)).Content.ReadAsByteArrayAsync());
        client.DefaultRequestHeaders.Authorization = null;
        await ErrorAsync(await client.GetAsync(detail.VerificationImage!.ContentUrl), HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
    }
    [Fact]
    public async Task TransferAndSamePeriodRenewalPreserveOwnershipAndRegistrationHistory()
    {
        var owner = await CreateUserAsync();
        var nextOwner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var id = await RegisterAsync();
        await LoginAsync(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest(nextOwner.IdentificationNumber.Value, "Transferencia"))).StatusCode);
        await using var context = fixture.CreateContext();
        var ownerships = await context.VehicleOwnerships.OrderBy(x => x.StartAt).ToArrayAsync();
        Assert.Equal(2, ownerships.Length);
        Assert.Equal(ownerships[0].EndAt, ownerships[1].StartAt);
        Assert.Equal(nextOwner.Id, ownerships[1].UserId);
        var old = await context.VehicleRegistrations.SingleAsync();
        Assert.Equal(VehicleRegistrationStatus.CANCELLED, old.Status);
        Assert.Equal("OWNERSHIP_TRANSFERRED", old.CancelReason);
        await LoginAsync(nextOwner);
        using var renewal = EmptyRenewal();
        var renewalResponse = await client.PostAsync($"/api/v1/vehicles/{id}/renew", renewal);
        Assert.True(renewalResponse.StatusCode == HttpStatusCode.Created, await renewalResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, await context.VehicleRegistrations.CountAsync());
        Assert.Equal(1, FileCount());
        using var duplicate = EmptyRenewal();
        await ErrorAsync(await client.PostAsync($"/api/v1/vehicles/{id}/renew", duplicate), HttpStatusCode.Conflict, "VEHICLE_REGISTRATION_ALREADY_EXISTS");
        await LoginAsync(owner);
        Assert.Empty((await client.GetFromJsonAsync<VehicleResponse[]>("/api/v1/vehicles/me"))!);
        await ErrorAsync(await client.GetAsync($"/api/v1/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
    }
    [Fact]
    public async Task NewPeriodDerivesExpiredWithoutChangingHistoricalRegistration_RenewsUsingExistingEvidence()
    {
        await LoginAsync(await CreateUserAsync());
        var id = await RegisterAsync();
        await using var context = fixture.CreateContext();
        (await context.AcademicPeriods.SingleAsync()).Close();
        await context.SaveChangesAsync();
        var next = new AcademicPeriod("2027-1", new(2027, 1, 1), new(2027, 6, 30), DateTimeOffset.UtcNow);
        next.Activate();
        context.AcademicPeriods.Add(next);
        await context.SaveChangesAsync();
        Assert.Equal("EXPIRED", (await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!.Vehicle.RegistrationState);
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, (await context.VehicleRegistrations.SingleAsync()).Status);
        using var renewal = EmptyRenewal();
        var renewalResponse = await client.PostAsync($"/api/v1/vehicles/{id}/renew", renewal);
        Assert.True(renewalResponse.StatusCode == HttpStatusCode.Created, await renewalResponse.Content.ReadAsStringAsync());
        Assert.Equal("ACTIVE", (await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!.Vehicle.RegistrationState);
        Assert.Equal(1, await context.Vehicles.CountAsync());
        Assert.Equal(1, await context.VehicleOwnerships.CountAsync());
        Assert.Equal(2, await context.VehicleRegistrations.CountAsync());
    }
    [Fact]
    public async Task ActivationDeactivationAreIdempotent_UpdateCannotChangeIdentifiers()
    {
        await LoginAsync(await CreateUserAsync());
        var id = await RegisterAsync();
        foreach (var action in new[] { "deactivate", "deactivate", "activate", "activate" })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsync($"/api/v1/vehicles/{id}/{action}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/vehicles/{id}", new UpdateVehicleRequest("Updated", "Updated", "Red"))).StatusCode);
        await ErrorAsync(await client.PutAsJsonAsync($"/api/v1/vehicles/{id}", new { brand = "Attack", model = "Model", color = "Red", plate = "HACKED" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "VEHICLE_DEACTIVATED"));
        Assert.Equal(1, await context.AuditLogs.CountAsync(x => x.Action == "VEHICLE_ACTIVATED"));
        Assert.Equal("ABC123", (await context.Vehicles.SingleAsync()).Plate!.Value);
    }
    [Fact]
    public async Task AdminSearchFiltersAndIdentifierCorrectionWork()
    {
        var owner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var id = await RegisterAsync();
        await LoginAsync(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/v1/vehicles/{id}/identifier", new CorrectVehicleIdentifierRequest(" new-456 ", "Corrección"))).StatusCode);
        var page = (await client.GetFromJsonAsync<PagedResponse<VehicleResponse>>($"/api/v1/vehicles?search=new456&type=MOTORCYCLE&status=ACTIVE&registrationState=ACTIVE&ownerIdentificationNumber={owner.IdentificationNumber.Value}&pageSize=1"))!;
        Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);
        await using var context = fixture.CreateContext();
        var audit = await context.AuditLogs.SingleAsync(x => x.Action == "VEHICLE_IDENTIFIER_CORRECTED");
        Assert.Contains("ABC123", audit.OldValues!);
        Assert.Contains("NEW456", audit.NewValues!);
    }
    [Fact]
    public async Task AuditFailureRollsBackVehicleAndCompensatesOnlyNewFiles()
    {
        var owner = await CreateUserAsync();
        await LoginAsync(owner);
        await RegisterAsync();
        var historicalFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order().ToArray();
        await using var failingFactory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditLogRepository>();
            services.AddScoped<IAuditLogRepository, InvalidAuditRepository>();
        });
        using var failing = failingFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(owner, failing);
        using var body = Form(identifier: "NEW456");
        await ErrorAsync(await failing.PostAsync("/api/v1/vehicles", body), HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Vehicles.CountAsync());
        Assert.Equal(1, await context.VehicleOwnerships.CountAsync());
        Assert.Equal(1, await context.VehicleRegistrations.CountAsync());
        Assert.Equal(2, await context.AuditLogs.CountAsync());
        Assert.Equal(historicalFiles, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order().ToArray());
    }
    [Fact]
    public async Task SwaggerDocumentsMultipartRegistrationAndRenewal()
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/vehicles").GetProperty("post").GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        var fields = paths.GetProperty("/api/v1/vehicles").GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("multipart/form-data").GetProperty("schema").GetProperty("properties");
        Assert.Equal("binary", fields.GetProperty("VerificationImage.File").GetProperty("format").GetString());
        Assert.False(fields.TryGetProperty("Documents[1].File", out _)); Assert.False(fields.TryGetProperty("VerificationImage.Type", out _));
        Assert.False(fields.TryGetProperty("Photos", out _));
        Assert.True(paths.GetProperty("/api/v1/vehicles/{id}/renew").GetProperty("post").GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        Assert.True(paths.TryGetProperty("/api/v1/vehicles/{vehicleId}/photos/{photoId}/content", out _));
        Assert.Contains("binary", document.RootElement.GetRawText());
    }
    private sealed class InvalidAuditRepository(AppDbContext context) : IAuditLogRepository
    {
        public async Task AddAsync(AuditLog audit, CancellationToken cancellationToken) =>
            await context.AuditLogs.AddAsync(new AuditLog(Guid.NewGuid(), audit.Action, audit.EntityType, audit.EntityId, audit.CreatedAt), cancellationToken);
    }
}
