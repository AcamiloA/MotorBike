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
    public DateTimeOffset? DeletedAt { get; private set; }

    public Vehicle(VehicleType type, VehiclePlate? plate, FrameNumber? frameNumber,
        string brand, string model, string color, DateTimeOffset createdAt)
    {
        Type = Guard.Defined(type);
        if (type is VehicleType.BICYCLE or VehicleType.SCOOTER ? plate is not null || frameNumber is null : plate is null || frameNumber is not null)
            throw new DomainException("VALIDATION_ERROR", "El identificador no corresponde al tipo de vehículo.");
        Plate = plate;
        FrameNumber = frameNumber;
        Brand = Guard.Text(brand, 100, "marca").ToUpperInvariant();
        Model = Guard.Text(model, 100, "modelo").ToUpperInvariant();
        Color = Guard.Text(color, 100, "color").ToUpperInvariant();
        Status = VehicleStatus.ACTIVE;
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
    }

    public void UpdateDescription(string brand, string model, string color, DateTimeOffset updatedAt)
    {
        EnsureNotDeleted();
        var validBrand = Guard.Text(brand, 100, "marca").ToUpperInvariant();
        var validModel = Guard.Text(model, 100, "modelo").ToUpperInvariant();
        var validColor = Guard.Text(color, 100, "color").ToUpperInvariant();
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        Brand = validBrand;
        Model = validModel;
        Color = validColor;
        UpdatedAt = now;
    }

    public void CorrectIdentifier(string identifier, DateTimeOffset updatedAt)
    {
        EnsureNotDeleted();
        var plate = Type is VehicleType.BICYCLE or VehicleType.SCOOTER ? null : new VehiclePlate(identifier);
        var frame = Type is VehicleType.BICYCLE or VehicleType.SCOOTER ? new FrameNumber(identifier) : null;
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        Plate = plate;
        FrameNumber = frame;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now) => SetStatus(VehicleStatus.ACTIVE, now);
    public void Deactivate(DateTimeOffset now) => SetStatus(VehicleStatus.INACTIVE, now);
    public void Archive(DateTimeOffset now)
    {
        EnsureNotDeleted();
        var utc = Guard.Utc(now); Guard.Chronology(utc, UpdatedAt);
        DeletedAt = UpdatedAt = utc; Status = VehicleStatus.INACTIVE;
    }
    private void EnsureNotDeleted()
    {
        if (DeletedAt.HasValue) throw new DomainException("VEHICLE_ARCHIVED", "El vehículo está archivado.");
    }

    private void SetStatus(VehicleStatus status, DateTimeOffset now)
    {
        EnsureNotDeleted();
        if (Status == status) return;
        var utc = Guard.Utc(now);
        Guard.Chronology(utc, UpdatedAt);
        Status = status;
        UpdatedAt = utc;
    }
}
