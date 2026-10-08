using UniversityParking.Domain.Common;
using UniversityParking.Domain.Incidents;

namespace UniversityParking.Domain.Tests.Incidents;

public sealed class IncidentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static Incident Create() => new(Guid.NewGuid(), Guid.NewGuid(), IncidentType.SECURITY, " Puerta dañada ", Now, Now);

    [Fact]
    public void GeneralIncident_ShouldAllowMissingUserVehicleAndMovement_AndStartOpen()
    {
        var incident = Create();
        Assert.Equal(IncidentStatus.OPEN, incident.Status);
        Assert.Equal("Puerta dañada", incident.Description);
        Assert.Null(incident.UserId);
        Assert.Null(incident.VehicleId);
        Assert.Null(incident.ParkingMovementId);
        Assert.Null(incident.ResolvedAt);
        Assert.Null(incident.ResolvedBy);
    }

    [Fact]
    public void Resolve_ShouldStoreResolutionAndActor_AndRejectAnotherResolution()
    {
        var incident = Create();
        var admin = Guid.NewGuid();
        incident.Resolve(" Reparada ", admin, Now.AddHours(1));
        Assert.Equal(IncidentStatus.RESOLVED, incident.Status);
        Assert.Equal("Reparada", incident.Resolution);
        Assert.Equal(admin, incident.ResolvedBy);
        Assert.Equal(Now.AddHours(1), incident.ResolvedAt);
        Assert.Equal("INCIDENT_ALREADY_RESOLVED", Assert.Throws<DomainException>(() => incident.Resolve("Otra", admin, Now.AddHours(2))).Code);
        Assert.Equal("INCIDENT_NOT_OPEN", Assert.Throws<DomainException>(() => incident.Cancel(Now.AddHours(2))).Code);
    }

    [Fact]
    public void Resolve_ShouldRejectMissingDetails_WithoutChangingState()
    {
        var incident = Create();
        Assert.Throws<DomainException>(() => incident.Resolve(" ", Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => incident.Resolve("Reparada", Guid.Empty, Now));
        Assert.Equal(IncidentStatus.OPEN, incident.Status);
        Assert.Null(incident.Resolution);
        Assert.Null(incident.ResolvedBy);
        Assert.Null(incident.ResolvedAt);
    }

    [Fact]
    public void Cancel_ShouldBeIdempotent_AndPreventResolution()
    {
        var incident = Create();
        incident.Cancel(Now.AddMinutes(1), "Reporte duplicado");
        incident.Cancel(Now.AddMinutes(2), "Otro motivo");
        Assert.Equal(IncidentStatus.CANCELLED, incident.Status);
        Assert.Equal("Reporte duplicado", incident.Resolution);
        Assert.Equal(Now.AddMinutes(1), incident.UpdatedAt);
        Assert.Null(incident.ResolvedAt);
        Assert.Throws<DomainException>(() => incident.Resolve("Reparada", Guid.NewGuid(), Now.AddMinutes(3)));
    }

    [Fact]
    public void Incident_ShouldRequireLotReporterDescriptionAndValidType()
    {
        Assert.Throws<DomainException>(() => new Incident(Guid.Empty, Guid.NewGuid(), IncidentType.OTHER, "Texto", Now, Now));
        Assert.Throws<DomainException>(() => new Incident(Guid.NewGuid(), Guid.Empty, IncidentType.OTHER, "Texto", Now, Now));
        Assert.Throws<DomainException>(() => new Incident(Guid.NewGuid(), Guid.NewGuid(), IncidentType.OTHER, " ", Now, Now));
        Assert.Throws<DomainException>(() => new Incident(Guid.NewGuid(), Guid.NewGuid(), (IncidentType)99, "Texto", Now, Now));
    }
}
