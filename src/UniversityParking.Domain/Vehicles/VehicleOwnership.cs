using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles;

public sealed class VehicleOwnership : Entity
{
    public Guid VehicleId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset? EndAt { get; private set; }
    public string? TransferReason { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public VehicleOwnership(Guid vehicleId, Guid userId, DateTimeOffset startAt, Guid createdBy, string? transferReason = null)
    {
        VehicleId = Guard.Id(vehicleId, "vehículo");
        UserId = Guard.Id(userId, "propietario");
        StartAt = CreatedAt = Guard.Utc(startAt);
        CreatedBy = Guard.Id(createdBy, "actor");
        TransferReason = Guard.OptionalText(transferReason, 500, "motivo de transferencia");
    }

    public void Close(DateTimeOffset endAt, string reason)
    {
        if (EndAt.HasValue)
            throw new DomainException("INVALID_VEHICLE_OWNER", "La propiedad ya terminó.");
        var utc = Guard.Utc(endAt);
        Guard.Chronology(utc, StartAt);
        var validReason = Guard.Text(reason, 500, "motivo de transferencia");
        EndAt = utc;
        TransferReason = validReason;
    }
}
