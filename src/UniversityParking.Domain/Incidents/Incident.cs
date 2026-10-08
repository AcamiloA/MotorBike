using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Incidents;

public sealed class Incident : Entity
{
    public Guid? UserId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? ParkingMovementId { get; private set; }
    public Guid ParkingLotId { get; private set; }
    public Guid ReportedBy { get; private set; }
    public IncidentType Type { get; private set; }
    public string Description { get; private set; }
    public IncidentStatus Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Resolution { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Incident(Guid parkingLotId, Guid reportedBy, IncidentType type, string description,
        DateTimeOffset occurredAt, DateTimeOffset createdAt, Guid? userId = null,
        Guid? vehicleId = null, Guid? parkingMovementId = null)
    {
        ParkingLotId = Guard.Id(parkingLotId, "parqueadero");
        ReportedBy = Guard.Id(reportedBy, "reportante");
        Type = Guard.Defined(type);
        Description = Guard.Text(description, int.MaxValue, "descripción");
        OccurredAt = Guard.Utc(occurredAt);
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
        UserId = Guard.OptionalId(userId, "usuario");
        VehicleId = Guard.OptionalId(vehicleId, "vehículo");
        ParkingMovementId = Guard.OptionalId(parkingMovementId, "movimiento");
        Status = IncidentStatus.OPEN;
    }

    public void Resolve(string resolution, Guid resolvedBy, DateTimeOffset resolvedAt)
    {
        if (Status == IncidentStatus.RESOLVED)
            throw new DomainException("INCIDENT_ALREADY_RESOLVED", "El incidente ya está resuelto.");
        RequireOpen();
        var text = Guard.Text(resolution, int.MaxValue, "resolución");
        var actor = Guard.Id(resolvedBy, "actor");
        var now = Guard.Utc(resolvedAt);
        Guard.Chronology(now, UpdatedAt);
        Resolution = text;
        ResolvedBy = actor;
        ResolvedAt = UpdatedAt = now;
        Status = IncidentStatus.RESOLVED;
    }

    public void Cancel(DateTimeOffset cancelledAt, string? reason = null)
    {
        if (Status == IncidentStatus.CANCELLED) return;
        RequireOpen();
        var text = Guard.OptionalText(reason, int.MaxValue, "motivo de cancelación");
        var now = Guard.Utc(cancelledAt);
        Guard.Chronology(now, UpdatedAt);
        Resolution = text;
        UpdatedAt = now;
        Status = IncidentStatus.CANCELLED;
    }

    private void RequireOpen()
    {
        if (Status != IncidentStatus.OPEN)
            throw new DomainException("INCIDENT_NOT_OPEN", "El incidente no está abierto.");
    }
}
