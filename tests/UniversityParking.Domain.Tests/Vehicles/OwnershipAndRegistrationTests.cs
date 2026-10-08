using UniversityParking.Domain.Common;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Domain.Tests.Vehicles;

public sealed class OwnershipAndRegistrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ownership_ShouldCloseAtTransferTime_AndPreserveOwnerHistory()
    {
        var vehicle = Guid.NewGuid();
        var oldOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var old = new VehicleOwnership(vehicle, oldOwner, Now, oldOwner);
        var transferTime = Now.AddHours(1);
        old.Close(transferTime, " Venta ");
        var current = new VehicleOwnership(vehicle, newOwner, transferTime, admin, "Venta");
        Assert.Equal(oldOwner, old.UserId);
        Assert.Equal(transferTime, old.EndAt);
        Assert.Equal("Venta", old.TransferReason);
        Assert.Equal(old.EndAt, current.StartAt);
        Assert.Equal(newOwner, current.UserId);
        Assert.Equal(admin, current.CreatedBy);
        Assert.Null(current.EndAt);
    }

    [Fact]
    public void Ownership_ShouldRejectInvalidClose_WithoutChangingOpenOwnership()
    {
        var owner = Guid.NewGuid();
        var ownership = new VehicleOwnership(Guid.NewGuid(), owner, Now, owner);
        Assert.Throws<DomainException>(() => ownership.Close(Now.AddSeconds(-1), "Venta"));
        Assert.Throws<DomainException>(() => ownership.Close(Now, " "));
        Assert.Null(ownership.EndAt);
        ownership.Close(Now, "Venta");
        Assert.Throws<DomainException>(() => ownership.Close(Now.AddHours(1), "Otra venta"));
        Assert.Equal(Now, ownership.EndAt);
    }

    [Fact]
    public void Registration_ShouldStartActive_AndStoreCancellationDetails()
    {
        var registration = new VehicleRegistration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, registration.Status);
        Assert.Null(registration.CancelledAt);
        Assert.Null(registration.CancelledBy);
        Assert.Null(registration.CancelReason);
        var admin = Guid.NewGuid();
        registration.Cancel(Now.AddHours(1), admin, "OWNERSHIP_TRANSFERRED");
        Assert.Equal(VehicleRegistrationStatus.CANCELLED, registration.Status);
        Assert.Equal(admin, registration.CancelledBy);
        Assert.Equal(Now.AddHours(1), registration.CancelledAt);
        Assert.Equal("OWNERSHIP_TRANSFERRED", registration.CancelReason);
        Assert.Equal("VEHICLE_REGISTRATION_CANCELLED", Assert.Throws<DomainException>(() => registration.Cancel(Now.AddHours(2), admin, "Otro motivo")).Code);
    }

    [Fact]
    public void Registration_ShouldRejectIncompleteCancellation_WithoutMutatingStatus()
    {
        var registration = new VehicleRegistration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Throws<DomainException>(() => registration.Cancel(Now, Guid.Empty, "Transferencia"));
        Assert.Throws<DomainException>(() => registration.Cancel(Now, Guid.NewGuid(), " "));
        Assert.Throws<DomainException>(() => registration.Cancel(Now.AddSeconds(-1), Guid.NewGuid(), "Transferencia"));
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, registration.Status);
        Assert.Null(registration.CancelledAt);
    }

    [Fact]
    public void Registration_ShouldRepresentDifferentOwnersInSamePeriod()
    {
        var vehicle = Guid.NewGuid();
        var period = Guid.NewGuid();
        var first = new VehicleRegistration(vehicle, Guid.NewGuid(), period, Now);
        first.Cancel(Now.AddHours(1), Guid.NewGuid(), "OWNERSHIP_TRANSFERRED");
        var second = new VehicleRegistration(vehicle, Guid.NewGuid(), period, Now.AddHours(1));
        Assert.Equal(first.VehicleId, second.VehicleId);
        Assert.Equal(first.AcademicPeriodId, second.AcademicPeriodId);
        Assert.NotEqual(first.UserId, second.UserId);
        Assert.Equal(VehicleRegistrationStatus.ACTIVE, second.Status);
    }
}
