using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles;

public enum VehicleVerificationImageType { TRANSIT_LICENSE_FRONT, BICYCLE_PHOTO }

public sealed class VehicleVerificationImage : Entity
{
    public Guid VehicleId { get; private set; }
    public VehicleVerificationImageType Type { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private VehicleVerificationImage() { }
    public VehicleVerificationImage(Vehicle vehicle, string key, string name, string mime, long size, DateTimeOffset now)
    {
        VehicleId = Guard.Id(vehicle.Id, "vehículo"); Type = ForVehicle(vehicle.Type);
        CreatedAt = UpdatedAt = Guard.Utc(now); SetMetadata(key, name, mime, size);
    }
    public static VehicleVerificationImageType ForVehicle(VehicleType type) => Guard.Defined(type) == VehicleType.BICYCLE
        ? VehicleVerificationImageType.BICYCLE_PHOTO : VehicleVerificationImageType.TRANSIT_LICENSE_FRONT;
    public void EnsureCompatible(VehicleType vehicleType)
    {
        if (Type != ForVehicle(vehicleType)) throw new DomainException("VALIDATION_ERROR", "La evidencia no corresponde al tipo de vehículo.");
    }
    public void Replace(Vehicle vehicle, string key, string name, string mime, long size, DateTimeOffset now)
    {
        if (vehicle.Id != VehicleId) throw new DomainException("VALIDATION_ERROR", "La evidencia pertenece a otro vehículo.");
        EnsureCompatible(vehicle.Type); var utc = Guard.Utc(now); Guard.Chronology(utc, UpdatedAt);
        SetMetadata(key, name, mime, size); UpdatedAt = utc;
    }
    private void SetMetadata(string key, string name, string mime, long size)
    {
        var storageKey = FileMetadata.StorageKey(key); var fileName = Guard.Text(name, 255, "nombre del archivo");
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Any(char.IsControl))
            throw new DomainException("VALIDATION_ERROR", "El nombre del archivo no es válido.");
        var contentType = FileMetadata.ContentType(mime, allowPdf: false); var sizeBytes = FileMetadata.Size(size, 5 * 1024 * 1024);
        StorageKey = storageKey; OriginalFileName = fileName; ContentType = contentType; SizeBytes = sizeBytes;
    }
}
