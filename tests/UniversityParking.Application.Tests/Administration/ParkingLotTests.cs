using UniversityParking.Application.ParkingLots;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Tests.Administration;

public sealed class ParkingLotTests
{
    private readonly AdministrationTestContext context = new();
    private CreateParkingLotCommandHandler Create => new(context.Operation, context, context.Work);
    private static CreateParkingLotCommand Request(string campus = "Kennedy") => new("Principal", campus, new(6, 0), new(22, 0));
    [Fact]
    public async Task CreateAddsExactlyThreeActiveStandardZonesInSameSave()
    {
        var id = (await Create.Handle(Request(), default)).Value;
        Assert.Equal(3, context.Zones.Count);
        Assert.Equal(new[] { VehicleType.CAR, VehicleType.MOTORCYCLE, VehicleType.BICYCLE }, context.Zones.Select(x => x.VehicleType));
        Assert.All(context.Zones, x => { Assert.Equal(id, x.ParkingLotId); Assert.Equal(ParkingZoneStatus.ACTIVE, x.Status); Assert.Equal(context.Lots[0].CreatedAt, x.CreatedAt); });
        Assert.Equal(1, context.Work.SaveCount);
        Assert.Equal("PARKING_LOT_CREATED", Assert.Single(context.Audits).Action);
    }
    [Theory]
    [InlineData(22, 6)]
    [InlineData(6, 6)]
    public void OvernightOrEqualScheduleIsRejected(int opening, int closing) => Assert.False(new CreateParkingLotCommandValidator()
        .Validate(Request() with { OpeningTime = new(opening, 0), ClosingTime = new(closing, 0) }).IsValid);
    [Theory]
    [InlineData("USER")]
    [InlineData("GUARD")]
    public async Task CreateRequiresAdmin(string role)
    {
        context.Actor.Roles = [role];
        Assert.Equal("FORBIDDEN", (await Create.Handle(Request(), default)).Error!.Code);
        Assert.Empty(context.Zones);
    }
    [Fact]
    public async Task NameAndCampusMustBeUnique()
    {
        await Create.Handle(Request(), default);
        Assert.Equal("PARKING_LOT_ALREADY_EXISTS", (await Create.Handle(Request() with { Name = " Principal " }, default)).Error!.Code);
        Assert.True((await Create.Handle(Request("Otra sede"), default)).IsSuccess);
    }
    [Fact]
    public async Task DeactivationWithOpenMovementIsRejectedWithoutAudit()
    {
        var id = (await Create.Handle(Request(), default)).Value;
        context.LotHasOpenMovement = true;
        var handler = new DeactivateParkingLotCommandHandler(context.Operation, context, context, context.Work);
        Assert.Equal("PARKING_LOT_HAS_OPEN_MOVEMENTS", (await handler.Handle(new(id), default)).Error!.Code);
        Assert.Equal(ParkingLotStatus.ACTIVE, context.Lots[0].Status);
        Assert.Single(context.Audits);
    }
    [Fact]
    public async Task ActivationAndDeactivationAreIdempotentWithoutReactivatingZones()
    {
        var id = (await Create.Handle(Request(), default)).Value;
        context.Zones[0].Deactivate(context.Clock.UtcNow);
        var deactivate = new DeactivateParkingLotCommandHandler(context.Operation, context, context, context.Work);
        var activate = new ActivateParkingLotCommandHandler(context.Operation, context, context.Work);
        for (var i = 0; i < 2; i++) Assert.True((await deactivate.Handle(new(id), default)).IsSuccess);
        for (var i = 0; i < 2; i++) Assert.True((await activate.Handle(new(id), default)).IsSuccess);
        Assert.Equal(3, context.Work.SaveCount);
        Assert.Equal(ParkingZoneStatus.INACTIVE, context.Zones[0].Status);
    }
    [Fact]
    public async Task UpdatePreservesZoneIdentitiesAndTypes()
    {
        var id = (await Create.Handle(Request(), default)).Value;
        var zones = context.Zones.Select(x => (x.Id, x.VehicleType)).ToArray();
        var handler = new UpdateParkingLotCommandHandler(context.Operation, context, context.Work);
        Assert.True((await handler.Handle(new(id, "Nuevo nombre", "Otra sede", new(7, 0), new(21, 0)), default)).IsSuccess);
        Assert.Equal(zones, context.Zones.Select(x => (x.Id, x.VehicleType)));
        Assert.Equal("PARKING_LOT_UPDATED", context.Audits.Last().Action);
    }
    [Fact]
    public async Task GuardQueryReceivesOnlyActiveLots_AdminQueryIncludesInactive()
    {
        await Create.Handle(Request(), default);
        var inactive = (await Create.Handle(Request("Otra sede"), default)).Value;
        context.Lots.Single(x => x.Id == inactive).Deactivate(context.Clock.UtcNow);
        var all = await new GetParkingLotsQueryHandler(context.Operation, context).Handle(new(), default);
        Assert.Equal(2, all.Value.TotalCount);
        context.Actor.Roles = ["USER", "GUARD"];
        context.Roles.Codes = ["USER", "GUARD"];
        var active = await new GetActiveParkingLotsQueryHandler(context.Operation, context).Handle(new(), default);
        Assert.Single(active.Value.Items);
        Assert.Equal("FORBIDDEN", (await new GetParkingLotsQueryHandler(context.Operation, context).Handle(new(), default)).Error!.Code);
    }
}
