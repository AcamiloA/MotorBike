using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles;

public sealed class VehicleDocument : Entity
{
    public Guid VehicleId { get; private set; }
    public VehicleDocumentType Type { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string StorageKey { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public DateOnly? IssuedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public VehicleDocument(Guid vehicleId, VehicleDocumentType type, string storageKey,
        string originalFileName, string contentType, long sizeBytes, DateTimeOffset createdAt,
        string? documentNumber = null, DateOnly? issuedOn = null, DateOnly? expiresOn = null)
    {
        if (issuedOn.HasValue && expiresOn.HasValue && expiresOn < issuedOn)
            throw new DomainException("VALIDATION_ERROR", "El vencimiento no puede ser anterior a la emisión.");
        VehicleId = Guard.Id(vehicleId, "vehículo");
        Type = Guard.Defined(type);
        DocumentNumber = Guard.OptionalText(documentNumber, 100, "número del documento");
        StorageKey = FileMetadata.StorageKey(storageKey);
        OriginalFileName = Guard.Text(originalFileName, 255, "nombre del archivo");
        ContentType = FileMetadata.ContentType(contentType, allowPdf: true);
        SizeBytes = FileMetadata.Size(sizeBytes, 10 * 1024 * 1024);
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        CreatedAt = Guard.Utc(createdAt);
    }
}
