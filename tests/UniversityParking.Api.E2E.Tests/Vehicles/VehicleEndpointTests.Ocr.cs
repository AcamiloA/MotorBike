using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Vehicles;

namespace UniversityParking.Api.E2E.Tests.Vehicles;

public sealed partial class VehicleEndpointTests
{
    [Theory][InlineData("valid", 201)][InlineData("invalid", 400)][InlineData("review", 400)][InlineData("unreadable", 400)][InlineData("unavailable", 503)]
    public async Task OcrGatesRegisterAndReplaceWithoutChangingPreviousData(string mode, int status)
    {
        var owner = await CreateUserAsync(); await LoginAsync(owner); var id = await RegisterAsync();
        var ocr = new TestDocumentTextExtractor();
        if (mode == "invalid") ocr.Lines = ["FACTURA SUPERMERCADO PRECIO PRODUCTOS TOTAL COMPRA PAGADA EFECTIVO"];
        if (mode == "review") ocr.Confidence = 60;
        if (mode == "unreadable") ocr.Lines = [];
        if (mode == "unavailable") ocr.Fail = true;
        await using var otherFactory = CreateFactory(s => { s.RemoveAll<IDocumentTextExtractor>(); s.AddSingleton<IDocumentTextExtractor>(ocr); });
        using var other = otherFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); await LoginAsync(owner, other);
        await using var db = fixture.CreateContext(); var old = await db.VehicleVerificationImages.AsNoTracking().SingleAsync();
        using var registration = Form(identifier: "NEW456"); var response = await other.PostAsync("/api/v1/vehicles", registration);
        Assert.Equal(status, (int)response.StatusCode);
        var code = mode switch { "invalid" => "TRANSIT_LICENSE_INVALID_FORMAT", "review" => "TRANSIT_LICENSE_REVIEW_REQUIRED", "unreadable" => "TRANSIT_LICENSE_UNREADABLE", _ => "DOCUMENT_OCR_UNAVAILABLE" };
        if (mode != "valid") { await ErrorAsync(response, (HttpStatusCode)status, code); Assert.Single(await db.Vehicles.ToArrayAsync()); Assert.Equal(1, FileCount()); }
        using var replacement = VerificationForm(); var replace = await other.PutAsync($"/api/v1/vehicles/{id}/verification-image", replacement);
        Assert.Equal(mode == "valid" ? 204 : status, (int)replace.StatusCode);
        var after = await db.VehicleVerificationImages.AsNoTracking().SingleAsync(x => x.VehicleId == id);
        if (mode != "valid") { await ErrorAsync(replace, (HttpStatusCode)status, code); Assert.Equal(old.StorageKey, after.StorageKey); Assert.Equal(old.UpdatedAt, after.UpdatedAt); }
        else Assert.NotEqual(old.StorageKey, after.StorageKey);
        Assert.Equal(2, ocr.Calls); Assert.DoesNotContain("LICENCIA DE TRANSITO", string.Join(" ", await db.AuditLogs.Select(x => x.NewValues).ToArrayAsync()));
        Assert.Equal("ABC123", (await db.Vehicles.SingleAsync(x => x.Id == id)).Plate!.Value);
    }
    [Fact]
    public async Task BicycleRegistrationAndReplacementNeverCallExtractor()
    {
        var ocr = new TestDocumentTextExtractor { Fail = true }; var owner = await CreateUserAsync();
        await using var otherFactory = CreateFactory(s => { s.RemoveAll<IDocumentTextExtractor>(); s.AddSingleton<IDocumentTextExtractor>(ocr); });
        using var other = otherFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); await LoginAsync(owner, other);
        using var form = Form("BICYCLE", "FRAMEOCR"); var response = await other.PostAsync("/api/v1/vehicles", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var id = (await response.Content.ReadFromJsonAsync<VehicleCreatedResponse>())!.Id;
        using var replacement = VerificationForm(); Assert.Equal(HttpStatusCode.NoContent, (await other.PutAsync($"/api/v1/vehicles/{id}/verification-image", replacement)).StatusCode);
        Assert.Equal(0, ocr.Calls);
    }
}
