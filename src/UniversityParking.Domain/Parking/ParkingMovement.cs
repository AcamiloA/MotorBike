using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Parking;

public sealed class ParkingMovement : Entity
{
    public Guid UserId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid ParkingLotId { get; private set; }
    public Guid ParkingZoneId { get; private set; }
    public DateTimeOffset CheckInAt { get; private set; }
    public Guid CheckInGuardId { get; private set; }
    public DateTimeOffset? CheckOutAt { get; private set; }
    public Guid? CheckOutGuardId { get; private set; }
    public ParkingMovementStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public ParkingMovement(Guid userId, Guid vehicleId, Guid parkingLotId, Guid parkingZoneId,
        DateTimeOffset checkInAt, Guid checkInGuardId, Guid? movementId = null) : base(movementId ?? Guid.NewGuid())
    {
        UserId = Guard.Id(userId, "usuario");
        VehicleId = Guard.Id(vehicleId, "vehículo");
        ParkingLotId = Guard.Id(parkingLotId, "parqueadero");
        ParkingZoneId = Guard.Id(parkingZoneId, "zona");
        CheckInGuardId = Guard.Id(checkInGuardId, "celador");
        CheckInAt = CreatedAt = UpdatedAt = Guard.Utc(checkInAt);
        Status = ParkingMovementStatus.OPEN;
    }

    private ParkingMovement() { }

    public void Close(DateTimeOffset checkOutAt, Guid guardId)
    {
        if (Status != ParkingMovementStatus.OPEN)
            throw new DomainException("VEHICLE_NOT_INSIDE", "El vehículo no tiene un movimiento abierto.");
        var now = Guard.Utc(checkOutAt);
        Guard.Chronology(now, CheckInAt);
        var actor = Guard.Id(guardId, "celador");
        CheckOutAt = UpdatedAt = now;
        CheckOutGuardId = actor;
        Status = ParkingMovementStatus.CLOSED;
    }

    public TimeSpan GetDuration(DateTimeOffset now)
    {
        var end = CheckOutAt ?? Guard.Utc(now);
        Guard.Chronology(end, CheckInAt);
        return end - CheckInAt;
    }
}
