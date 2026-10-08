using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.AcademicPeriods;

public enum AcademicPeriodStatus { PLANNED, ACTIVE, CLOSED }

public sealed class AcademicPeriod : Entity
{
    public string Name { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public AcademicPeriodStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public AcademicPeriod(string name, DateOnly startsOn, DateOnly endsOn, DateTimeOffset createdAt)
    {
        if (startsOn >= endsOn)
            throw new DomainException("VALIDATION_ERROR", "El inicio del periodo debe ser anterior al fin.");
        Name = Guard.Text(name, 50, "nombre del periodo");
        StartsOn = startsOn;
        EndsOn = endsOn;
        Status = AcademicPeriodStatus.PLANNED;
        CreatedAt = Guard.Utc(createdAt);
    }

    public void Activate()
    {
        if (Status == AcademicPeriodStatus.CLOSED)
            throw new DomainException("ACADEMIC_PERIOD_NOT_ACTIVE", "Un periodo cerrado no puede reactivarse.");
        Status = AcademicPeriodStatus.ACTIVE;
    }

    public void Close()
    {
        if (Status == AcademicPeriodStatus.CLOSED) return;
        if (Status != AcademicPeriodStatus.ACTIVE)
            throw new DomainException("ACADEMIC_PERIOD_NOT_ACTIVE", "Solo puede cerrarse un periodo activo.");
        Status = AcademicPeriodStatus.CLOSED;
    }
}
