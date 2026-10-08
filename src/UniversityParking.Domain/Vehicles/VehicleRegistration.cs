using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles;

public sealed class VehicleRegistration : Entity
{
    public Guid VehicleId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AcademicPeriodId { get; private set; }
    public VehicleRegistrationStatus Status { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancelReason { get; private set; }

    public VehicleRegistration(Guid vehicleId, Guid userId, Guid academicPeriodId, DateTimeOffset registeredAt)
    {
        VehicleId = Guard.Id(vehicleId, "vehículo");
        UserId = Guard.Id(userId, "usuario");
        AcademicPeriodId = Guard.Id(academicPeriodId, "periodo");
        RegisteredAt = Guard.Utc(registeredAt);
        Status = VehicleRegistrationStatus.ACTIVE;
    }

    public void Cancel(DateTimeOffset cancelledAt, Guid cancelledBy, string reason)
    {
        if (Status == VehicleRegistrationStatus.CANCELLED)
            throw new DomainException("VEHICLE_REGISTRATION_CANCELLED", "El registro ya está cancelado.");
        var now = Guard.Utc(cancelledAt);
        Guard.Chronology(now, RegisteredAt);
        var actor = Guard.Id(cancelledBy, "actor");
        var validReason = Guard.Text(reason, 200, "motivo de cancelación");
        CancelledAt = now;
        CancelledBy = actor;
        CancelReason = validReason;
        Status = VehicleRegistrationStatus.CANCELLED;
    }
}
