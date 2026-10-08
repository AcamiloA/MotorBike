using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles;

public sealed class VehiclePhoto : Entity
{
    public Guid VehicleId { get; private set; }
    public VehiclePhotoType Type { get; private set; }
    public string StorageKey { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public VehiclePhoto(Guid vehicleId, VehiclePhotoType type, string storageKey,
        string originalFileName, string contentType, long sizeBytes, DateTimeOffset createdAt)
    {
        VehicleId = Guard.Id(vehicleId, "vehículo");
        Type = Guard.Defined(type);
        StorageKey = FileMetadata.StorageKey(storageKey);
        OriginalFileName = Guard.Text(originalFileName, 255, "nombre del archivo");
        ContentType = FileMetadata.ContentType(contentType, allowPdf: false);
        SizeBytes = FileMetadata.Size(sizeBytes, 5 * 1024 * 1024);
        CreatedAt = Guard.Utc(createdAt);
    }
}
