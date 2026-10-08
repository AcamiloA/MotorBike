using UniversityParking.Domain.Common;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Domain.Tests.Vehicles;

public sealed class FileMetadataTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static VehiclePhoto Photo(string type = "image/jpeg", long size = 100, string key = "vehicles/123/photos/456.jpg") =>
        new(Guid.NewGuid(), VehiclePhotoType.GENERAL, key, "original.jpg", type, size, Now);

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    public void Photo_ShouldAcceptSupportedTypesAtSizeLimit(string type)
    {
        var photo = Photo(type, 5 * 1024 * 1024);
        Assert.Equal(VehiclePhotoType.GENERAL, photo.Type);
        Assert.Equal(type, photo.ContentType);
        Assert.Equal(5 * 1024 * 1024, photo.SizeBytes);
    }

    [Fact]
    public void Photo_ShouldRejectPdfOversizeAndEmptyFiles()
    {
        Assert.Equal("FILE_TYPE_NOT_ALLOWED", Assert.Throws<DomainException>(() => Photo("application/pdf")).Code);
        Assert.Equal("FILE_TOO_LARGE", Assert.Throws<DomainException>(() => Photo(size: 5 * 1024 * 1024 + 1)).Code);
        Assert.Equal("VALIDATION_ERROR", Assert.Throws<DomainException>(() => Photo(size: 0)).Code);
    }

    [Theory]
    [InlineData("../secret.jpg")]
    [InlineData("/absolute/photo.jpg")]
    [InlineData("C:/secret.jpg")]
    [InlineData("vehicles/../secret.jpg")]
    [InlineData("vehicles\\secret.jpg")]
    public void FileMetadata_ShouldRejectUnsafeStorageKeys(string key) => Assert.Throws<DomainException>(() => Photo(key: key));

    [Fact]
    public void ExpiredDocument_ShouldRemainValidSupportMetadata()
    {
        var document = new VehicleDocument(Guid.NewGuid(), VehicleDocumentType.INSURANCE, "vehicles/123/documents/456.pdf",
            "seguro.pdf", "application/pdf", 10 * 1024 * 1024, Now, " A12 ", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        Assert.Equal("A12", document.DocumentNumber);
        Assert.Equal(new DateOnly(2025, 12, 31), document.ExpiresOn);
    }

    [Fact]
    public void Document_ShouldAllowOptionalDatesAndNumber_AndRejectReversedDates()
    {
        var document = new VehicleDocument(Guid.NewGuid(), VehicleDocumentType.OWNERSHIP_SUPPORT, "vehicles/123/documents/456.pdf",
            "soporte.pdf", "application/pdf", 100, Now);
        Assert.Null(document.DocumentNumber);
        Assert.Null(document.IssuedOn);
        Assert.Null(document.ExpiresOn);
        Assert.Throws<DomainException>(() => new VehicleDocument(Guid.NewGuid(), VehicleDocumentType.INSURANCE,
            "vehicles/123/documents/456.pdf", "seguro.pdf", "application/pdf", 100, Now,
            issuedOn: new DateOnly(2026, 2, 1), expiresOn: new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void DocumentAndAttachment_ShouldEnforceTheirOwnSizeLimits()
    {
        Assert.Equal("FILE_TOO_LARGE", Assert.Throws<DomainException>(() => new VehicleDocument(Guid.NewGuid(),
            VehicleDocumentType.INSURANCE, "vehicles/123/documents/456.pdf", "seguro.pdf", "application/pdf", 10 * 1024 * 1024 + 1, Now)).Code);
        var attachment = new IncidentAttachment(Guid.NewGuid(), "incidents/123/attachments/456.pdf", "evidencia.pdf",
            "application/pdf", 10 * 1024 * 1024, Now);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal("FILE_TOO_LARGE", Assert.Throws<DomainException>(() => new IncidentAttachment(Guid.NewGuid(),
            "incidents/123/attachments/456.pdf", "evidencia.pdf", "application/pdf", 10 * 1024 * 1024 + 1, Now)).Code);
        Assert.Equal("FILE_TYPE_NOT_ALLOWED", Assert.Throws<DomainException>(() => new IncidentAttachment(Guid.NewGuid(),
            "incidents/123/attachments/456.exe", "evidencia.exe", "application/octet-stream", 100, Now)).Code);
    }
}
