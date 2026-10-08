using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles;

public static class VehicleDocumentPolicy
{
    public static IReadOnlyList<VehicleDocumentType> GetRequiredDocuments(VehicleType type) => type switch
    {
        VehicleType.CAR or VehicleType.MOTORCYCLE => Array.AsReadOnly(new[]
            { VehicleDocumentType.VEHICLE_REGISTRATION, VehicleDocumentType.INSURANCE }),
        VehicleType.BICYCLE => Array.AsReadOnly(new[] { VehicleDocumentType.OWNERSHIP_SUPPORT }),
        _ => throw new DomainException("VALIDATION_ERROR", "El tipo de vehículo no es válido.")
    };

    public static bool HasRequiredDocuments(VehicleType type, IEnumerable<VehicleDocumentType> available)
    {
        ArgumentNullException.ThrowIfNull(available);
        var types = available.ToHashSet();
        return GetRequiredDocuments(type).All(types.Contains);
    }
}
