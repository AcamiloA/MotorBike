using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Parking;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Tests.Parking;

public sealed class ParkingCommandTests
{
    [Theory]
    [InlineData(VehicleType.CAR)]
    [InlineData(VehicleType.MOTORCYCLE)]
    [InlineData(VehicleType.BICYCLE)]
    public async Task CheckInUsesAuthenticatedGuardAndAutomaticallySelectedZone(VehicleType type)
    {
        var context = new ParkingTestContext(type);
        var result = await context.CheckIn.Handle(context.Request, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(context.Store.User.Id, result.Value.CheckInGuardId);
        Assert.Equal(context.Zones.Single(x => x.VehicleType == type).Id, result.Value.ParkingZoneId);
        Assert.Equal(context.Clock.UtcNow, result.Value.CheckInAtUtc);
        Assert.Equal(TimeSpan.Zero, result.Value.Duration);
        Assert.Equal(1, context.Store.Commits);
        Assert.Equal("PARKING_CHECK_IN", Assert.Single(context.Store.Audits).Action);
    }
    [Theory]
    [InlineData("user-inactive", "USER_INACTIVE")]
    [InlineData("vehicle-inactive", "VEHICLE_INACTIVE")]
    [InlineData("not-owned", "VEHICLE_NOT_OWNED_BY_USER")]
    [InlineData("no-owner", "VEHICLE_OWNERSHIP_NOT_FOUND")]
    [InlineData("no-period", "ACADEMIC_PERIOD_NOT_ACTIVE")]
    [InlineData("no-registration", "VEHICLE_REGISTRATION_REQUIRED")]
    [InlineData("cancelled", "VEHICLE_REGISTRATION_CANCELLED")]
    [InlineData("lot-inactive", "PARKING_LOT_INACTIVE")]
    [InlineData("zone-inactive", "PARKING_ZONE_NOT_AVAILABLE")]
    [InlineData("zone-missing", "PARKING_ZONE_NOT_AVAILABLE")]
    public async Task FailedEntryRuleCannotPersistMovement(string condition, string code)
    {
        var context = new ParkingTestContext();
        switch (condition)
        {
            case "user-inactive": context.Target.Deactivate(context.Clock.UtcNow); break;
            case "vehicle-inactive": context.Vehicle.Deactivate(context.Clock.UtcNow); break;
            case "not-owned": context.Store.Ownerships[0].Close(context.Clock.UtcNow, "Transfer"); context.Store.Ownerships.Add(new(context.Vehicle.Id, context.Store.User.Id, context.Clock.UtcNow, context.Store.User.Id)); break;
            case "no-owner": context.Store.Ownerships.Clear(); break;
            case "no-period": context.Store.Period = null; break;
            case "no-registration": context.Store.Registrations.Clear(); break;
            case "cancelled": context.Store.Registrations[0].Cancel(context.Clock.UtcNow, context.Store.User.Id, "Cancellation"); break;
            case "lot-inactive": context.Lot.Deactivate(context.Clock.UtcNow); break;
            case "zone-inactive": context.Zones.Single(x => x.VehicleType == context.Vehicle.Type).Deactivate(context.Clock.UtcNow); break;
            case "zone-missing": context.Zones.RemoveAll(x => x.VehicleType == context.Vehicle.Type); break;
        }
        Assert.Equal(code, (await context.CheckIn.Handle(context.Request, default)).Error!.Code);
        Assert.Empty(context.Store.Movements);
        Assert.Empty(context.Store.Audits);
        Assert.Equal(0, context.Store.Saves);
    }
    [Theory]
    [InlineData(10, 59, 59, false)]
    [InlineData(11, 0, 0, true)]
    [InlineData(2, 59, 59, true)]
    [InlineData(3, 0, 0, false)]
    public async Task ExactBogotaBoundariesAreEnforced(int hour, int minute, int second, bool allowed)
    {
        var context = new ParkingTestContext();
        context.Clock.UtcNow = new(2026, 10, hour < 10 ? 8 : 7, hour, minute, second, TimeSpan.Zero);
        var result = await context.CheckIn.Handle(context.Request, default);
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed) Assert.Equal("PARKING_LOT_CLOSED", result.Error!.Code);
    }
    [Theory]
    [InlineData("USER")]
    [InlineData("ADMIN")]
    public async Task BothCommandsRequireGuard_AdminDoesNotImplyGuard(string role)
    {
        var context = new ParkingTestContext();
        context.Store.Roles = ["USER", role];
        Assert.Equal("FORBIDDEN", (await context.CheckIn.Handle(context.Request, default)).Error!.Code);
        Assert.Equal("FORBIDDEN", (await context.CheckOut.Handle(new(context.Vehicle.Id), default)).Error!.Code);
    }
    [Fact]
    public async Task UnauthenticatedActorCannotCheckInOrOut()
    {
        var context = new ParkingTestContext();
        context.Store.IsAuthenticated = false;
        Assert.Equal(ErrorType.Unauthorized, (await context.CheckIn.Handle(context.Request, default)).Error!.Type);
        Assert.Equal(ErrorType.Unauthorized, (await context.CheckOut.Handle(new(context.Vehicle.Id), default)).Error!.Type);
    }
    [Fact]
    public async Task StudentCarInconsistentDataIsRejectedByBackend()
    {
        var context = new ParkingTestContext(VehicleType.CAR);
        context.Target.Update(context.Target.FullName, context.Target.UniversityId, "Ingeniería", MemberType.STUDENT, context.Target.CardCode, context.Clock.UtcNow);
        Assert.Equal("STUDENT_CANNOT_REGISTER_CAR", (await context.CheckIn.Handle(context.Request, default)).Error!.Code);
    }
    [Fact]
    public async Task DuplicateEntryRejectsSameVehicleAndAnotherVehicleOfSameUser()
    {
        var context = new ParkingTestContext();
        await context.CheckIn.Handle(context.Request, default);
        Assert.Equal("VEHICLE_ALREADY_INSIDE", (await context.CheckIn.Handle(context.Request, default)).Error!.Code);
        var second = new Vehicle(VehicleType.MOTORCYCLE, new Domain.Vehicles.ValueObjects.VehiclePlate("DEF456"), null, "Brand", "Model", "Black", context.Clock.UtcNow);
        context.Store.Vehicles.Add(second);
        context.Store.Ownerships.Add(new(second.Id, context.Target.Id, context.Clock.UtcNow, context.Store.User.Id));
        context.Store.Registrations.Add(new(second.Id, context.Target.Id, context.Store.Period!.Id, context.Clock.UtcNow));
        Assert.Equal("USER_ALREADY_HAS_VEHICLE_INSIDE", (await context.CheckIn.Handle(context.Request with { VehicleId = second.Id }, default)).Error!.Code);
        Assert.Single(context.Store.Movements);
    }
    [Theory]
    [InlineData("outside-hours")]
    [InlineData("user-inactive")]
    [InlineData("vehicle-inactive")]
    [InlineData("period-closed")]
    [InlineData("registration-cancelled")]
    [InlineData("lot-inactive")]
    public async Task CheckoutDoesNotRevalidateEntryRequirements(string condition)
    {
        var context = new ParkingTestContext();
        var entered = (await context.CheckIn.Handle(context.Request, default)).Value;
        context.Clock.UtcNow = context.Clock.UtcNow.AddHours(1);
        switch (condition)
        {
            case "outside-hours": context.Clock.UtcNow = new(2026, 10, 8, 4, 0, 0, TimeSpan.Zero); break;
            case "user-inactive": context.Target.Deactivate(context.Clock.UtcNow); break;
            case "vehicle-inactive": context.Vehicle.Deactivate(context.Clock.UtcNow); break;
            case "period-closed": context.Store.Period!.Close(); break;
            case "registration-cancelled": context.Store.Registrations[0].Cancel(context.Clock.UtcNow, context.Store.User.Id, "Cancellation"); break;
            case "lot-inactive": context.Lot.Deactivate(context.Clock.UtcNow); break;
        }
        var result = await context.CheckOut.Handle(new(context.Vehicle.Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(ParkingMovementStatus.CLOSED, result.Value.Status);
        Assert.Equal(context.Store.User.Id, result.Value.CheckOutGuardId);
        Assert.Equal(context.Clock.UtcNow - entered.CheckInAtUtc, result.Value.Duration);
        Assert.Equal("PARKING_CHECK_OUT", context.Store.Audits.Last().Action);
        Assert.Equal("VEHICLE_NOT_INSIDE", (await context.CheckOut.Handle(new(context.Vehicle.Id), default)).Error!.Code);
    }
    [Fact]
    public async Task CheckoutRejectsMissingVehicleOrNoOpenMovement()
    {
        var context = new ParkingTestContext();
        Assert.Equal("VEHICLE_NOT_FOUND", (await context.CheckOut.Handle(new(Guid.NewGuid()), default)).Error!.Code);
        Assert.Equal("VEHICLE_NOT_INSIDE", (await context.CheckOut.Handle(new(context.Vehicle.Id), default)).Error!.Code);
    }
    [Fact]
    public async Task LookupShowsEligibleVehicle_ThenCurrentMovementAndNoEligibleVehicles()
    {
        var context = new ParkingTestContext();
        var lookup = new GetParkingAccessUserQueryHandler(context.Operation, context.Store, context.Store, context, context.Store, context.Store, context.Clock);
        var before = await lookup.Handle(new("target-card", null), default);
        Assert.Single(before.Value.EligibleVehicles);
        await context.CheckIn.Handle(context.Request, default);
        var after = await lookup.Handle(new(null, "target-id"), default);
        Assert.NotNull(after.Value.CurrentMovement);
        Assert.Empty(after.Value.EligibleVehicles);
    }
    [Theory]
    [InlineData(null, null)]
    [InlineData("card", "id")]
    [InlineData(" ", " ")]
    public void LookupRequiresExactlyOneIdentifier(string? card, string? id) =>
        Assert.False(new GetParkingAccessUserQueryValidator().Validate(new GetParkingAccessUserQuery(card, id)).IsValid);
    [Fact]
    public async Task PersonalHistoryForcesSessionUserAndConvertsInclusiveBogotaDates()
    {
        var context = new ParkingTestContext();
        var handler = new GetMyParkingHistoryQueryHandler(context.Operation, context.Store, context, context, context.Clock);
        await handler.Handle(new(new(2026, 10, 7), new(2026, 10, 7)), default);
        Assert.Equal(context.Store.User.Id, context.LastFilter!.UserId);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 5, 0, 0, TimeSpan.Zero), context.LastFilter.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 5, 0, 0, TimeSpan.Zero), context.LastFilter.UntilUtc);
    }
}
