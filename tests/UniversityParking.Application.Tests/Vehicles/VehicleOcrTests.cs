using Microsoft.Extensions.Logging.Abstractions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Documents;
using UniversityParking.Application.Files;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Tests.Vehicles;

public sealed partial class VehicleRegistrationTests
{
    private RegisterVehicleCommandHandler WithOcr(TestOcr ocr) => new(Context, store, store, store, store, store, store,
        new FileUploadValidator(), store, NullLogger<UploadedFileBatch>.Instance, new TransitLicenseValidationService(ocr, new TransitLicenseFormatValidator()));
    [Theory][InlineData(VehicleType.CAR, 1)][InlineData(VehicleType.MOTORCYCLE, 1)][InlineData(VehicleType.BICYCLE, 0)]
    public async Task OcrAppliesOnlyToMotorVehiclesAndDoesNotChangePlate(VehicleType type, int calls)
    {
        store.ChangeMember(MemberType.STAFF); var ocr = new TestOcr(); var result = await WithOcr(ocr).Handle(Request(type), default);
        Assert.True(result.IsSuccess); Assert.Equal(calls, ocr.Calls); Assert.Single(store.Vehicles); Assert.Single(store.VerificationImages);
        Assert.Equal(type == VehicleType.BICYCLE ? null : "ABC123", store.Vehicles.Single().Plate?.Value);
    }
    [Theory][InlineData("invalid", "TRANSIT_LICENSE_INVALID_FORMAT")][InlineData("review", "TRANSIT_LICENSE_REVIEW_REQUIRED")][InlineData("unreadable", "TRANSIT_LICENSE_UNREADABLE")][InlineData("unavailable", "DOCUMENT_OCR_UNAVAILABLE")]
    public async Task RejectedOcrNeverAddsEntitiesOrUploads(string mode, string code)
    {
        var ocr = Rejected(mode); var result = await WithOcr(ocr).Handle(Request(), default);
        Assert.Equal(code, result.Error!.Code); Assert.Empty(store.Vehicles); Assert.Empty(store.Ownerships); Assert.Empty(store.Registrations);
        Assert.Empty(store.VerificationImages); Assert.Empty(store.Keys); Assert.Empty(store.Audits); Assert.Equal(0, store.Saves);
    }
    [Theory][InlineData("invalid")][InlineData("review")][InlineData("unreadable")][InlineData("unavailable")]
    public async Task RejectedReplacementPreservesPreviousEvidence(string mode)
    {
        var id = (await Handler.Handle(Request(), default)).Value; var before = store.VerificationImages.Single(); var key = before.StorageKey; var time = before.UpdatedAt;
        var handler = new UpdateVehicleVerificationImageCommandHandler(Context, store, store, new FileUploadValidator(), store,
            NullLogger<UploadedFileBatch>.Instance, new TransitLicenseValidationService(Rejected(mode), new TransitLicenseFormatValidator()));
        Assert.True((await handler.Handle(new(id, Request().VerificationImage), default)).IsFailure);
        Assert.Equal(key, before.StorageKey); Assert.Equal(time, before.UpdatedAt); Assert.Equal(key, Assert.Single(store.Keys)); Assert.Equal(1, store.Saves);
    }
    private static TestOcr Rejected(string mode) => new()
    {
        Unavailable = mode == "unavailable",
        Text = mode switch
        {
            "review" => TestOcr.ValidText(60),
            "invalid" => new([new("FACTURA SUPERMERCADO PRODUCTOS PRECIO TOTAL COMPRA PAGADA EFECTIVO", 99)], []),
            _ => new([], [])
        }
    };
}
