using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Vehicles;

public sealed partial class VehicleEndpointTests
{
    private async Task AddLegacyAsync(Guid id)
    {
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        var photo = $"vehicles/{id}/photos/legacy.png"; var doc = $"vehicles/{id}/documents/legacy.pdf";
        await storage.UploadAsync(new(photo, new MemoryStream(Png), "image/png", Png.Length), default);
        await storage.UploadAsync(new(doc, new MemoryStream(Pdf), "application/pdf", Pdf.Length), default);
        await using var db = fixture.CreateContext();
        db.VehiclePhotos.Add(new(id, VehiclePhotoType.GENERAL, photo, "legacy.png", "image/png", Png.Length, DateTimeOffset.UtcNow));
        db.VehicleDocuments.Add(new(id, VehicleDocumentType.VEHICLE_REGISTRATION, doc, "legacy.pdf", "application/pdf", Pdf.Length, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }
    private static MultipartFormDataContent VerificationForm(byte[]? bytes = null, string mime = "image/png", string name = "new.png")
    {
        var form = new MultipartFormDataContent(); var file = new ByteArrayContent(bytes ?? Png);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime); form.Add(file, "VerificationImage.File", name); return form;
    }
    [Fact]
    public async Task JpegIsAcceptedAndPdfIsRejectedForSingleEvidence()
    {
        await LoginAsync(await CreateUserAsync());
        using var pdf = Form(photoBytes: Pdf, photoName: "license.pdf", photoMime: "application/pdf");
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", pdf), HttpStatusCode.BadRequest, "FILE_TYPE_NOT_ALLOWED");
        Assert.Equal(0, FileCount());
        using var jpeg = Form(photoBytes: [255, 216, 255, 224, 0, 1], photoName: "license.jpg", photoMime: "image/jpeg");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/api/v1/vehicles", jpeg)).StatusCode);
        await using var db = fixture.CreateContext(); var image = await db.VehicleVerificationImages.SingleAsync();
        Assert.Equal("image/jpeg", image.ContentType); Assert.Equal(VehicleVerificationImageType.TRANSIT_LICENSE_FRONT, image.Type);
    }
    [Fact]
    public async Task UploadFailureDuringReplacePreservesExistingEvidenceAndRemovesAcceptedNewBytes()
    {
        var user = await CreateUserAsync(); await LoginAsync(user); var id = await RegisterAsync();
        await using var db = fixture.CreateContext(); var old = await db.VehicleVerificationImages.AsNoTracking().SingleAsync();
        var actual = factory.Services.GetRequiredService<IFileStorage>();
        await using var broken = CreateFactory(s => { s.RemoveAll<IFileStorage>(); s.AddSingleton<IFileStorage>(new FailingStorage(actual)); });
        using var other = broken.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); await LoginAsync(user, other);
        using var form = VerificationForm(); Assert.Equal(HttpStatusCode.InternalServerError, (await other.PutAsync($"/api/v1/vehicles/{id}/verification-image", form)).StatusCode);
        Assert.Equal(old.StorageKey, (await db.VehicleVerificationImages.AsNoTracking().SingleAsync()).StorageKey); Assert.Equal(1, FileCount());
        Assert.Equal(Png, await (await client.GetAsync($"/api/v1/vehicles/{id}/verification-image/content")).Content.ReadAsByteArrayAsync());
    }
    [Theory][InlineData("VerificationImage.Type")][InlineData("VerificationImageType")][InlineData("Photos[0].Type")]
    public async Task ClientCannotChooseVerificationType(string field)
    {
        await LoginAsync(await CreateUserAsync()); using var form = Form(); form.Add(new StringContent("BICYCLE_PHOTO"), field);
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", form), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await using var db = fixture.CreateContext(); Assert.Empty(await db.Vehicles.ToListAsync()); Assert.Equal(0, FileCount());
    }
    [Fact]
    public async Task TwoVerificationFilesAreRejected()
    {
        await LoginAsync(await CreateUserAsync()); using var form = Form();
        form.Add(new ByteArrayContent(Png), "VerificationImage.File", "second.png");
        await ErrorAsync(await client.PostAsync("/api/v1/vehicles", form), HttpStatusCode.BadRequest, "VALIDATION_ERROR"); Assert.Equal(0, FileCount());
    }
    [Fact]
    public async Task ReplaceKeepsSingleRowChangesBytesAndDeletesOldOnlyAfterCommit()
    {
        await LoginAsync(await CreateUserAsync()); var id = await RegisterAsync();
        await using var db = fixture.CreateContext(); var before = await db.VehicleVerificationImages.AsNoTracking().SingleAsync();
        using var form = VerificationForm(Png.Concat(new byte[] { 1, 2 }).ToArray());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/v1/vehicles/{id}/verification-image", form)).StatusCode);
        var after = await db.VehicleVerificationImages.AsNoTracking().SingleAsync(); Assert.Equal(before.Id, after.Id); Assert.NotEqual(before.StorageKey, after.StorageKey);
        Assert.Equal(before.CreatedAt, after.CreatedAt); Assert.Equal(1, FileCount());
        Assert.Equal("TRANSIT_LICENSE_FRONT", after.Type.ToString()); Assert.Single(await db.AuditLogs.Where(x => x.Action == "VEHICLE_VERIFICATION_IMAGE_UPDATED").ToArrayAsync());
        Assert.DoesNotContain(after.StorageKey, (await db.AuditLogs.OrderByDescending(x => x.CreatedAt).FirstAsync()).NewValues!);
    }
    [Fact]
    public async Task DatabaseFailureDuringReplacePreservesOldRowAndFile()
    {
        var user = await CreateUserAsync(); await LoginAsync(user); var id = await RegisterAsync();
        await using var db = fixture.CreateContext(); var old = await db.VehicleVerificationImages.AsNoTracking().SingleAsync();
        await using var broken = CreateFactory(s => { s.RemoveAll<IAuditLogRepository>(); s.AddScoped<IAuditLogRepository, InvalidAuditRepository>(); });
        using var other = broken.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); await LoginAsync(user, other);
        using var form = VerificationForm(); Assert.Equal(HttpStatusCode.InternalServerError, (await other.PutAsync($"/api/v1/vehicles/{id}/verification-image", form)).StatusCode);
        Assert.Equal(old.StorageKey, (await db.VehicleVerificationImages.AsNoTracking().SingleAsync()).StorageKey); Assert.Equal(1, FileCount());
        Assert.Equal(Png, await (await client.GetAsync($"/api/v1/vehicles/{id}/verification-image/content")).Content.ReadAsByteArrayAsync());
    }
    [Theory][InlineData("USER", 404)][InlineData("GUARD", 404)][InlineData("ADMIN", 204)]
    public async Task ReplacementUsesOwnerOrAdminPermissions(string role, int status)
    {
        await LoginAsync(await CreateUserAsync()); var id = await RegisterAsync();
        await LoginAsync(await CreateUserAsync(MemberType.STAFF, role)); using var form = VerificationForm();
        Assert.Equal(status, (int)(await client.PutAsync($"/api/v1/vehicles/{id}/verification-image", form)).StatusCode);
    }
    [Fact]
    public async Task LegacyDoesNotInferEvidenceAndMustAddBeforeRenewing()
    {
        await LoginAsync(await CreateUserAsync()); var id = await RegisterAsync(); await AddLegacyAsync(id);
        await using var db = fixture.CreateContext(); await db.VehicleVerificationImages.ExecuteDeleteAsync();
        (await db.AcademicPeriods.SingleAsync()).Close(); var period = new UniversityParking.Domain.AcademicPeriods.AcademicPeriod("2027-1", new(2027, 1, 1), new(2027, 6, 30), DateTimeOffset.UtcNow); period.Activate(); db.AcademicPeriods.Add(period); await db.SaveChangesAsync();
        var detail = (await client.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!;
        Assert.Null(detail.VerificationImage); Assert.Null(detail.Vehicle.VerificationImagePreviewUrl); Assert.Single(detail.Photos); Assert.Single(detail.Documents);
        using var empty = EmptyRenewal(); await ErrorAsync(await client.PostAsync($"/api/v1/vehicles/{id}/renew", empty), HttpStatusCode.Conflict, "VEHICLE_VERIFICATION_IMAGE_REQUIRED");
        using var replacement = VerificationForm(); Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/v1/vehicles/{id}/verification-image", replacement)).StatusCode);
        using var retry = EmptyRenewal(); Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/v1/vehicles/{id}/renew", retry)).StatusCode);
        Assert.Single(await db.VehiclePhotos.ToArrayAsync()); Assert.Single(await db.VehicleDocuments.ToArrayAsync());
    }
}
