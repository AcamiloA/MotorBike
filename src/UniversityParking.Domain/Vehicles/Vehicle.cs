using UniversityParking.Domain.Common;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Domain.Vehicles;

public sealed class Vehicle : Entity
{
    public VehicleType Type { get; private set; }
    public VehiclePlate? Plate { get; private set; }
    public FrameNumber? FrameNumber { get; private set; }
    public string Brand { get; private set; }
    public string Model { get; private set; }
    public string Color { get; private set; }
    public VehicleStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Vehicle(VehicleType type, VehiclePlate? plate, FrameNumber? frameNumber,
        string brand, string model, string color, DateTimeOffset createdAt)
    {
        Type = Guard.Defined(type);
        if (type == VehicleType.BICYCLE ? plate is not null || frameNumber is null : plate is null || frameNumber is not null)
            throw new DomainException("VALIDATION_ERROR", "El identificador no corresponde al tipo de vehículo.");
        Plate = plate;
        FrameNumber = frameNumber;
        Brand = Guard.Text(brand, 100, "marca");
        Model = Guard.Text(model, 100, "modelo");
        Color = Guard.Text(color, 100, "color");
        Status = VehicleStatus.ACTIVE;
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
    }

    public void UpdateDescription(string brand, string model, string color, DateTimeOffset updatedAt)
    {
        var validBrand = Guard.Text(brand, 100, "marca");
        var validModel = Guard.Text(model, 100, "modelo");
        var validColor = Guard.Text(color, 100, "color");
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        Brand = validBrand;
        Model = validModel;
        Color = validColor;
        UpdatedAt = now;
    }

    public void CorrectIdentifier(string identifier, DateTimeOffset updatedAt)
    {
        var plate = Type == VehicleType.BICYCLE ? null : new VehiclePlate(identifier);
        var frame = Type == VehicleType.BICYCLE ? new FrameNumber(identifier) : null;
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        Plate = plate;
        FrameNumber = frame;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now) => SetStatus(VehicleStatus.ACTIVE, now);
    public void Deactivate(DateTimeOffset now) => SetStatus(VehicleStatus.INACTIVE, now);

    private void SetStatus(VehicleStatus status, DateTimeOffset now)
    {
        if (Status == status) return;
        var utc = Guard.Utc(now);
        Guard.Chronology(utc, UpdatedAt);
        Status = status;
        UpdatedAt = utc;
    }
}
