using UniversityParking.Domain.Common;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Domain.Tests.Parking;

public sealed class ParkingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static ParkingMovement Create() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Guid.NewGuid());
    private static ParkingLot CreateLot() => new("Parqueadero Principal", "Kennedy", new TimeOnly(6, 0), new TimeOnly(22, 0), Now);

    [Fact]
    public void Movement_ShouldStartOpen_WithOnlyCheckInData()
    {
        var movement = Create();
        Assert.Equal(ParkingMovementStatus.OPEN, movement.Status);
        Assert.Equal(Now, movement.CheckInAt);
        Assert.NotEqual(Guid.Empty, movement.CheckInGuardId);
        Assert.Null(movement.CheckOutAt);
        Assert.Null(movement.CheckOutGuardId);
        Assert.Equal(Now, movement.CreatedAt);
    }

    [Fact]
    public void Close_ShouldSetCheckoutActorAndTime_WithoutChangingCheckInHistory()
    {
        var movement = Create();
        var originalGuard = movement.CheckInGuardId;
        var guard = Guid.NewGuid();
        movement.Close(Now.AddMinutes(80), guard);
        Assert.Equal(ParkingMovementStatus.CLOSED, movement.Status);
        Assert.Equal(Now.AddMinutes(80), movement.CheckOutAt);
        Assert.Equal(guard, movement.CheckOutGuardId);
        Assert.Equal(originalGuard, movement.CheckInGuardId);
        Assert.Equal(Now, movement.CheckInAt);
        Assert.Equal(Now.AddMinutes(80), movement.UpdatedAt);
        Assert.Equal(TimeSpan.FromMinutes(80), movement.GetDuration(Now.AddDays(1)));
    }

    [Fact]
    public void ClosedMovement_ShouldRejectAnotherClose_AndRemainClosed()
    {
        var movement = Create();
        var guard = Guid.NewGuid();
        movement.Close(Now.AddMinutes(1), guard);
        var error = Assert.Throws<DomainException>(() => movement.Close(Now.AddMinutes(2), Guid.NewGuid()));
        Assert.Equal("VEHICLE_NOT_INSIDE", error.Code);
        Assert.Equal(ParkingMovementStatus.CLOSED, movement.Status);
        Assert.Equal(Now.AddMinutes(1), movement.CheckOutAt);
        Assert.Equal(guard, movement.CheckOutGuardId);
    }

    [Fact]
    public void Close_ShouldRejectTimeBeforeCheckIn_WithoutChangingMovement()
    {
        var movement = Create();
        Assert.Throws<DomainException>(() => movement.Close(Now.AddSeconds(-1), Guid.NewGuid()));
        Assert.Equal(ParkingMovementStatus.OPEN, movement.Status);
        Assert.Null(movement.CheckOutAt);
        Assert.Null(movement.CheckOutGuardId);
    }

    [Fact]
    public void Close_ShouldAllowSameInstantAsCheckIn()
    {
        var movement = Create();
        movement.Close(Now, Guid.NewGuid());
        Assert.Equal(TimeSpan.Zero, movement.GetDuration(Now));
    }

    [Fact]
    public void Close_ShouldRequireGuard_WithoutPartiallySettingCheckout()
    {
        var movement = Create();
        Assert.Throws<DomainException>(() => movement.Close(Now.AddMinutes(1), Guid.Empty));
        Assert.Null(movement.CheckOutAt);
        Assert.Equal(ParkingMovementStatus.OPEN, movement.Status);
    }

    [Fact]
    public void OpenDuration_ShouldBeDerivedFromSuppliedTime()
    {
        var movement = Create();
        Assert.Equal(TimeSpan.FromHours(2), movement.GetDuration(Now.AddHours(2)));
        Assert.Equal(TimeSpan.FromHours(3), movement.GetDuration(Now.AddHours(3)));
        Assert.Throws<DomainException>(() => movement.GetDuration(Now.AddSeconds(-1)));
        Assert.Equal(ParkingMovementStatus.OPEN, movement.Status);
    }

    [Fact]
    public void Movement_ShouldNormalizeOffsetToUtc()
    {
        var local = new DateTimeOffset(2026, 10, 7, 7, 0, 0, TimeSpan.FromHours(-5));
        var movement = new ParkingMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), local, Guid.NewGuid());
        Assert.Equal(Now, movement.CheckInAt);
        Assert.Equal(TimeSpan.Zero, movement.CheckInAt.Offset);
        movement.Close(local.AddHours(1), Guid.NewGuid());
        Assert.Equal(TimeSpan.Zero, movement.CheckOutAt!.Value.Offset);
    }

    [Theory]
    [InlineData(5, 59, 59, false)]
    [InlineData(6, 0, 0, true)]
    [InlineData(21, 59, 59, true)]
    [InlineData(22, 0, 0, false)]
    public void ParkingSchedule_ShouldUseInclusiveOpeningAndExclusiveClosing(int hour, int minute, int second, bool allowed) =>
        Assert.Equal(allowed, CreateLot().AllowsEntryAt(new TimeOnly(hour, minute, second)));

    [Theory]
    [InlineData(22, 5)]
    [InlineData(6, 6)]
    public void ParkingLot_ShouldRejectOvernightOrEmptySchedule(int opening, int closing) =>
        Assert.Throws<DomainException>(() => new ParkingLot("Principal", "Kennedy", new TimeOnly(opening, 0), new TimeOnly(closing, 0), Now));

    [Fact]
    public void ParkingLot_ShouldUpdateConfiguration_AndChangeStatusIdempotently()
    {
        var lot = CreateLot();
        lot.Update(" Nuevo ", " Centro ", new TimeOnly(7, 0), new TimeOnly(20, 0), Now.AddMinutes(1));
        Assert.Equal("Nuevo", lot.Name);
        Assert.Equal("Centro", lot.Campus);
        Assert.False(lot.AllowsEntryAt(new TimeOnly(6, 0)));
        lot.Deactivate(Now.AddMinutes(2));
        lot.Deactivate(Now.AddMinutes(3));
        Assert.Equal(ParkingLotStatus.INACTIVE, lot.Status);
        Assert.Equal(Now.AddMinutes(2), lot.UpdatedAt);
        lot.Activate(Now.AddMinutes(4));
        Assert.Equal(ParkingLotStatus.ACTIVE, lot.Status);
    }

    [Fact]
    public void Zone_ShouldRepresentCategory_AndPreserveLotAndTypeAcrossStatusChanges()
    {
        var lotId = Guid.NewGuid();
        var zone = new ParkingZone(lotId, "Zona de motos", VehicleType.MOTORCYCLE, Now);
        zone.Deactivate(Now.AddMinutes(1));
        zone.Activate(Now.AddMinutes(2));
        Assert.Equal(lotId, zone.ParkingLotId);
        Assert.Equal(VehicleType.MOTORCYCLE, zone.VehicleType);
        Assert.Equal(ParkingZoneStatus.ACTIVE, zone.Status);
    }

    [Fact]
    public void Movement_ShouldRejectMissingActorOrAssociations()
    {
        Assert.Throws<DomainException>(() => new ParkingMovement(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new ParkingMovement(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Now, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new ParkingMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Now, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new ParkingMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Now, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new ParkingMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Guid.Empty));
    }
}
