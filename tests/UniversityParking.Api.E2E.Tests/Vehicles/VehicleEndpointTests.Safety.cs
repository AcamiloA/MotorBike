using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Parking;
using UniversityParking.Infrastructure.Storage;

namespace UniversityParking.Api.E2E.Tests.Vehicles;

public sealed partial class VehicleEndpointTests
{
    [Fact]
    public async Task TransferAndDeactivationAreBlockedWhenVehicleIsInside()
    {
        var owner = await CreateUserAsync();
        var nextOwner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var id = await RegisterAsync();
        await using (var context = fixture.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            var lot = new ParkingLot("Test lot", "Kennedy", new(6, 0), new(22, 0), now);
            var zone = new ParkingZone(lot.Id, "Motos", VehicleType.MOTORCYCLE, now);
            context.AddRange(lot, zone, new ParkingMovement(owner.Id, id, lot.Id, zone.Id, now, admin.Id));
            await context.SaveChangesAsync();
        }
        Assert.True((await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!.Vehicle.IsInside);
        await ErrorAsync(await client.PatchAsync($"/api/v1/vehicles/{id}/deactivate", null), HttpStatusCode.Conflict, "VEHICLE_HAS_OPEN_PARKING_MOVEMENT");
        await LoginAsync(admin);
        await ErrorAsync(await client.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest(nextOwner.IdentificationNumber.Value, "Transfer")),
            HttpStatusCode.Conflict, "VEHICLE_HAS_OPEN_PARKING_MOVEMENT");
        await using var verification = fixture.CreateContext();
        Assert.Equal(owner.Id, (await verification.VehicleOwnerships.SingleAsync()).UserId);
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, (await verification.VehicleRegistrations.SingleAsync()).Status);
    }
    [Fact]
    public async Task CarCannotBeTransferredToStudent()
    {
        var owner = await CreateUserAsync(MemberType.TEACHER);
        var student = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var id = await RegisterAsync("CAR");
        await LoginAsync(admin);
        await ErrorAsync(await client.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest(student.IdentificationNumber.Value, "Transfer")),
            HttpStatusCode.Conflict, "STUDENT_CANNOT_REGISTER_CAR");
        await using var context = fixture.CreateContext();
        Assert.Equal(owner.Id, (await context.VehicleOwnerships.SingleAsync()).UserId);
        Assert.Equal(2, await context.AuditLogs.CountAsync());
    }
    [Theory]
    [InlineData("missing", HttpStatusCode.NotFound, "USER_NOT_FOUND")]
    [InlineData("inactive", HttpStatusCode.Conflict, "USER_INACTIVE")]
    [InlineData("same", HttpStatusCode.Conflict, "INVALID_VEHICLE_OWNER")]
    public async Task TransferRejectsInvalidNewOwner(string mode, HttpStatusCode status, string code)
    {
        var owner = await CreateUserAsync();
        var nextOwner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        if (mode == "inactive")
        {
            await using var context = fixture.CreateContext();
            (await context.Users.FindAsync(nextOwner.Id))!.Deactivate(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }
        await LoginAsync(owner);
        var id = await RegisterAsync();
        await LoginAsync(admin);
        var identification = mode == "same" ? owner.IdentificationNumber.Value : mode == "missing" ? "NOT-EXISTING" : nextOwner.IdentificationNumber.Value;
        await ErrorAsync(await client.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest(identification, "Transfer")), status, code);
        await using var verification = fixture.CreateContext();
        Assert.Single(await verification.VehicleOwnerships.ToArrayAsync());
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, (await verification.VehicleRegistrations.SingleAsync()).Status);
    }
    [Fact]
    public async Task TransferRequiresAdminEvenForOwner()
    {
        var owner = await CreateUserAsync();
        await LoginAsync(owner);
        var id = await RegisterAsync();
        await ErrorAsync(await client.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest("other", "Transfer")), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await client.PatchAsJsonAsync($"/api/v1/vehicles/{id}/identifier", new CorrectVehicleIdentifierRequest("NEW456", "Correction")), HttpStatusCode.Forbidden, "FORBIDDEN");
        await ErrorAsync(await client.GetAsync("/api/v1/vehicles"), HttpStatusCode.Forbidden, "FORBIDDEN");
    }
    [Fact]
    public async Task IdentifierCorrectionRejectsDuplicatesAndRequiresReason()
    {
        var owner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var first = await RegisterAsync();
        await RegisterAsync(identifier: "DEF456");
        await LoginAsync(admin);
        await ErrorAsync(await client.PatchAsJsonAsync($"/api/v1/vehicles/{first}/identifier", new CorrectVehicleIdentifierRequest("def-456", "Correction")),
            HttpStatusCode.Conflict, "VEHICLE_IDENTIFIER_ALREADY_EXISTS");
        await ErrorAsync(await client.PatchAsJsonAsync($"/api/v1/vehicles/{first}/identifier", new CorrectVehicleIdentifierRequest("NEW123", "")),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await using var context = fixture.CreateContext();
        Assert.False(await context.AuditLogs.AnyAsync(x => x.Action == "VEHICLE_IDENTIFIER_CORRECTED"));
    }
    [Fact]
    public async Task TransferRollsBackBothSavesWhenAuditFails()
    {
        var owner = await CreateUserAsync();
        var nextOwner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var id = await RegisterAsync();
        await using var failingFactory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditLogRepository>();
            services.AddScoped<IAuditLogRepository, InvalidAuditRepository>();
        });
        using var failing = failingFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(admin, failing);
        await ErrorAsync(await failing.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest(nextOwner.IdentificationNumber.Value, "Transfer")),
            HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
        await using var context = fixture.CreateContext();
        var ownership = await context.VehicleOwnerships.SingleAsync();
        Assert.Null(ownership.EndAt);
        Assert.Equal(owner.Id, ownership.UserId);
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, (await context.VehicleRegistrations.SingleAsync()).Status);
        Assert.Equal(3, FileCount());
    }
    [Fact]
    public async Task TransferCancelsOnlyRegistrationOfActivePeriod()
    {
        var owner = await CreateUserAsync();
        var nextOwner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var id = await RegisterAsync();
        Guid historicalId;
        Guid currentId;
        await using (var context = fixture.CreateContext())
        {
            historicalId = (await context.VehicleRegistrations.SingleAsync()).Id;
            (await context.AcademicPeriods.SingleAsync()).Close();
            await context.SaveChangesAsync();
            var next = new AcademicPeriod("2027-1", new(2027, 1, 1), new(2027, 6, 30), DateTimeOffset.UtcNow);
            next.Activate();
            var registration = new VehicleRegistration(id, owner.Id, next.Id, DateTimeOffset.UtcNow);
            currentId = registration.Id;
            context.AddRange(next, registration);
            await context.SaveChangesAsync();
        }
        await LoginAsync(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest(nextOwner.IdentificationNumber.Value, "Transfer"))).StatusCode);
        await using var verification = fixture.CreateContext();
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, (await verification.VehicleRegistrations.FindAsync(historicalId))!.Status);
        Assert.Equal(VehicleRegistrationStatus.CANCELLED, (await verification.VehicleRegistrations.FindAsync(currentId))!.Status);
    }
    [Fact]
    public async Task CancelledTripletCannotBeRecreatedAfterTransferBack()
    {
        var owner = await CreateUserAsync();
        var nextOwner = await CreateUserAsync();
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var id = await RegisterAsync();
        await LoginAsync(admin);
        foreach (var target in new[] { nextOwner, owner })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/vehicles/{id}/transfer", new TransferVehicleRequest(target.IdentificationNumber.Value, "Transfer"))).StatusCode);
        await LoginAsync(owner);
        using var body = EmptyRenewal();
        await ErrorAsync(await client.PostAsync($"/api/v1/vehicles/{id}/renew", body), HttpStatusCode.Conflict, "VEHICLE_REGISTRATION_CANCELLED");
    }
    [Fact]
    public async Task StudentCannotReactivateCarAfterAllowedMemberTypeChangeWhileCarInactive()
    {
        var owner = await CreateUserAsync(MemberType.TEACHER);
        var admin = await CreateUserAsync(MemberType.STAFF, "ADMIN");
        await LoginAsync(owner);
        var token = client.DefaultRequestHeaders.Authorization;
        var id = await RegisterAsync("CAR");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsync($"/api/v1/vehicles/{id}/deactivate", null)).StatusCode);
        await LoginAsync(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/users/{owner.Id}", new
        { fullName = owner.FullName, university = "ETITC", career = "Ingeniería", memberType = "STUDENT", cardCode = owner.CardCode.Value })).StatusCode);
        await ErrorAsync(await client.PatchAsync($"/api/v1/vehicles/{id}/activate", null), HttpStatusCode.Conflict, "STUDENT_CANNOT_REGISTER_CAR");
        client.DefaultRequestHeaders.Authorization = token;
        await ErrorAsync(await client.PatchAsync($"/api/v1/vehicles/{id}/activate", null), HttpStatusCode.Conflict, "STUDENT_CANNOT_REGISTER_CAR");
    }
    [Fact]
    public async Task ContentIdsMustBelongToRequestedVehicle()
    {
        await LoginAsync(await CreateUserAsync());
        var first = await RegisterAsync();
        var second = await RegisterAsync(identifier: "DEF456");
        var detail = (await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{second}"))!;
        await ErrorAsync(await client.GetAsync($"/api/v1/vehicles/{first}/photos/{detail.Photos[0].Id}/content"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await ErrorAsync(await client.GetAsync($"/api/v1/vehicles/{first}/documents/{detail.Documents[0].Id}/content"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
    }
    [Fact]
    public async Task MultipartCannotSupplyOwnerOrStorageKey()
    {
        await LoginAsync(await CreateUserAsync());
        using var body = Form();
        body.Add(new StringContent(Guid.NewGuid().ToString()), "OwnerUserId");
        body.Add(new StringContent("../../attack"), "StorageKey");
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", body), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(0, FileCount());
    }
    [Fact]
    public async Task ConcurrentDuplicatePlatesProduceOneVehicleAndNoOrphanFiles()
    {
        var first = await CreateUserAsync();
        var second = await CreateUserAsync();
        await LoginAsync(first);
        using var other = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(second, other);
        using var firstBody = Form();
        using var secondBody = Form(identifier: "abc-123");
        var responses = await Task.WhenAll(client.PostAsync("/api/v1/vehicles", firstBody), other.PostAsync("/api/v1/vehicles", secondBody));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        await ErrorAsync(Assert.Single(responses, x => x.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "VEHICLE_IDENTIFIER_ALREADY_EXISTS");
        await using var context = fixture.CreateContext();
        Assert.Single(await context.Vehicles.ToArrayAsync());
        Assert.Single(await context.VehicleOwnerships.ToArrayAsync());
        Assert.Single(await context.VehicleRegistrations.ToArrayAsync());
        Assert.Equal(3, FileCount());
    }
    [Fact]
    public async Task PartialStorageFailureRemovesPreviouslyUploadedFiles()
    {
        var owner = await CreateUserAsync();
        await using var failingFactory = CreateFactory(services =>
        {
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(new FailingStorage(new LocalFileStorage(new StorageOptions { LocalRootPath = root })));
        });
        using var failing = failingFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(owner, failing);
        using var body = Form();
        await ErrorAsync(await failing.PostAsync("/api/v1/vehicles", body), HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
        Assert.Equal(0, FileCount());
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.Vehicles.ToArrayAsync());
    }
    [Fact]
    public async Task RenewalAppendsDocumentsAndPreservesHistoricalSupportsEvenIfExpired()
    {
        await LoginAsync(await CreateUserAsync());
        var id = await RegisterAsync();
        await using (var context = fixture.CreateContext())
        {
            (await context.AcademicPeriods.SingleAsync()).Close();
            await context.SaveChangesAsync();
            var next = new AcademicPeriod("2027-1", new(2027, 1, 1), new(2027, 6, 30), DateTimeOffset.UtcNow);
            next.Activate();
            context.AcademicPeriods.Add(next);
            await context.SaveChangesAsync();
        }
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent("INSURANCE"), "Documents[0].Type");
        body.Add(new StringContent("NEW-POLICY"), "Documents[0].DocumentNumber");
        body.Add(new StringContent("2020-01-01"), "Documents[0].IssuedOn");
        body.Add(new StringContent("2021-01-01"), "Documents[0].ExpiresOn");
        var file = new ByteArrayContent(Pdf);
        file.Headers.ContentType = new("application/pdf");
        body.Add(file, "Documents[0].File", "new.pdf");
        var response = await client.PostAsync($"/api/v1/vehicles/{id}/renew", body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await using var verification = fixture.CreateContext();
        Assert.Equal(3, await verification.VehicleDocuments.CountAsync());
        Assert.Equal(4, FileCount());
        var document = await verification.VehicleDocuments.SingleAsync(x => x.DocumentNumber == "NEW-POLICY");
        Assert.Equal(new DateOnly(2021, 1, 1), document.ExpiresOn);
    }
    private sealed class FailingStorage(IFileStorage actual) : IFileStorage
    {
        private int uploads;
        public Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken ct) =>
            ++uploads == 2 ? throw new IOException("Storage test failure") : actual.UploadAsync(upload, ct);
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => actual.OpenReadAsync(key, ct);
        public Task DeleteAsync(string key, CancellationToken ct) => actual.DeleteAsync(key, ct);
        public Task<Uri?> GetReadUrlAsync(string key, CancellationToken ct) => actual.GetReadUrlAsync(key, ct);
    }
}
