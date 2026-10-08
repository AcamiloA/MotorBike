using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Parking;

public sealed class ParkingLot : Entity
{
    public string Name { get; private set; }
    public string Campus { get; private set; }
    public TimeOnly OpeningTime { get; private set; }
    public TimeOnly ClosingTime { get; private set; }
    public ParkingLotStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public ParkingLot(string name, string campus, TimeOnly openingTime, TimeOnly closingTime, DateTimeOffset createdAt)
    {
        ValidateSchedule(openingTime, closingTime);
        Name = Guard.Text(name, 150, "nombre del parqueadero");
        Campus = Guard.Text(campus, 150, "sede");
        OpeningTime = openingTime;
        ClosingTime = closingTime;
        Status = ParkingLotStatus.ACTIVE;
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
    }

    public bool AllowsEntryAt(TimeOnly localTime) => OpeningTime <= localTime && localTime < ClosingTime;

    public void Update(string name, string campus, TimeOnly openingTime, TimeOnly closingTime, DateTimeOffset updatedAt)
    {
        ValidateSchedule(openingTime, closingTime);
        var validName = Guard.Text(name, 150, "nombre del parqueadero");
        var validCampus = Guard.Text(campus, 150, "sede");
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        Name = validName;
        Campus = validCampus;
        OpeningTime = openingTime;
        ClosingTime = closingTime;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now) => SetStatus(ParkingLotStatus.ACTIVE, now);
    public void Deactivate(DateTimeOffset now) => SetStatus(ParkingLotStatus.INACTIVE, now);

    private void SetStatus(ParkingLotStatus status, DateTimeOffset now)
    {
        if (Status == status) return;
        var utc = Guard.Utc(now);
        Guard.Chronology(utc, UpdatedAt);
        Status = status;
        UpdatedAt = utc;
    }

    private static void ValidateSchedule(TimeOnly opening, TimeOnly closing)
    {
        if (opening >= closing)
            throw new DomainException("VALIDATION_ERROR", "La apertura debe ser anterior al cierre en el mismo día.");
    }
}
