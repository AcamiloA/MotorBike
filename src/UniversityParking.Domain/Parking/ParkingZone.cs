using UniversityParking.Domain.Common;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Domain.Parking;

public sealed class ParkingZone : Entity
{
    public Guid ParkingLotId { get; private set; }
    public string Name { get; private set; }
    public VehicleType VehicleType { get; private set; }
    public ParkingZoneStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public ParkingZone(Guid parkingLotId, string name, VehicleType vehicleType, DateTimeOffset createdAt)
    {
        ParkingLotId = Guard.Id(parkingLotId, "parqueadero");
        Name = Guard.Text(name, 150, "nombre de zona");
        VehicleType = Guard.Defined(vehicleType);
        Status = ParkingZoneStatus.ACTIVE;
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
    }

    public void Activate(DateTimeOffset now) => SetStatus(ParkingZoneStatus.ACTIVE, now);
    public void Deactivate(DateTimeOffset now) => SetStatus(ParkingZoneStatus.INACTIVE, now);

    private void SetStatus(ParkingZoneStatus status, DateTimeOffset now)
    {
        if (Status == status) return;
        var utc = Guard.Utc(now);
        Guard.Chronology(utc, UpdatedAt);
        Status = status;
        UpdatedAt = utc;
    }
}
