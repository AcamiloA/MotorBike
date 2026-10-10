using System.Text;
using UniversityParking.Application.Parking;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Tests.Parking;

public sealed class GuardAccessTests
{
    [Theory]
    [InlineData("MTAxNDI4NTU1Mw==", "1014285553")]
    [InlineData("  MTAxNDI4NTU1Mw==\r\n", "1014285553")]
    [InlineData("IDAwMSA=", "001")]
    [InlineData(" 1014285553 ", "1014285553")]
    [InlineData("12345678", "12345678")]
    [InlineData("ID-001", "ID-001")]
    [InlineData("AAAA", "AAAA")]
    public void ParserNormalizesInstitutionalIdentity(string raw, string identity)
        => Assert.Equal(identity, new QrIdentityParser().Parse(raw).Value.Value);

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("not/base64!")]
    [InlineData("role=ADMIN")] [InlineData("{\"UserId\":\"123\"}")]
    [InlineData("/w==")] [InlineData("wK8=")] [InlineData("IA==")]
    [InlineData("eyJyb2xlIjoiR1VBUkQifQ==")] [InlineData("VkVISUNMRUlEPTEx")]
    public void ParserRejectsInvalidOrStructuredInput(string? raw)
    {
        var result = new QrIdentityParser().Parse(raw);
        Assert.True(result.IsFailure); Assert.Equal("INVALID_QR_IDENTITY", result.Error!.Code);
    }
    [Fact] public void ParserBoundsBothRawAndDecodedPayloads()
    {
        var parser = new QrIdentityParser();
        Assert.True(parser.Parse(new('A', 257)).IsFailure);
        Assert.True(parser.Parse(new('1', 51)).IsFailure);
        Assert.True(parser.Parse(Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('1', 51)))).IsFailure);
        Assert.True(parser.Parse(Convert.ToBase64String(Encoding.UTF8.GetBytes("roles:ADMIN"))).IsFailure);
    }
    private static GetParkingAccessUserQueryHandler Lookup(ParkingTestContext c) => new(c.Operation,c.Store,c.Store,c,c.Store,c.Store,c.Clock,new QrIdentityParser());
    [Fact] public async Task RawPayloadMatchingAnotherCardCannotChangeResolvedIdentity()
    {
        var c = new ParkingTestContext();
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Target.IdentificationNumber.Value));
        c.Store.User.Update(c.Store.User.FullName,c.Store.User.UniversityId,c.Store.User.Career,c.Store.User.MemberType,new CardCode(payload),c.Store.UtcNow);
        var qr = (await Lookup(c).Handle(new(payload,null),default)).Value;
        var manual = (await Lookup(c).Handle(new(null,c.Target.IdentificationNumber.Value),default)).Value;
        Assert.Equal(c.Target.Id,qr.User.Id); Assert.Equal(manual.User,qr.User);
        Assert.Equal(manual.EligibleVehicles.Single(),qr.EligibleVehicles.Single());
        Assert.Null(qr.CurrentMovement);
    }
    [Theory] [InlineData(VehicleType.CAR)] [InlineData(VehicleType.MOTORCYCLE)] [InlineData(VehicleType.BICYCLE)]
    public async Task EntryRequiresCorrectAuthoritativeEvidenceAndLookupExplainsLegacy(VehicleType type)
    {
        var c = new ParkingTestContext(type);
        var valid = (await Lookup(c).Handle(new(null,"target-id"),default)).Value;
        Assert.Equal(VehicleVerificationImage.ForVehicle(type),Assert.Single(valid.EligibleVehicles).VerificationImage!.Type);
        c.Store.VerificationImages.Clear();
        var legacy = (await Lookup(c).Handle(new(null,"target-id"),default)).Value;
        Assert.Empty(legacy.EligibleVehicles); Assert.Equal("VEHICLE_VERIFICATION_REQUIRED",legacy.EntryBlockCode);
        Assert.Equal("VEHICLE_VERIFICATION_REQUIRED",(await c.CheckIn.Handle(c.Request,default)).Error!.Code);
        Assert.Empty(c.Store.Movements);
    }
    [Fact] public async Task OpenMovementWinsOverOtherVehicleAndMissingEvidence()
    {
        var c = new ParkingTestContext(); var first = (await c.CheckIn.Handle(c.Request,default)).Value;
        c.Store.VerificationImages.Clear(); c.Vehicle.Deactivate(c.Clock.UtcNow); c.Target.Deactivate(c.Clock.UtcNow);
        var access = (await Lookup(c).Handle(new(null,"target-id"),default)).Value;
        Assert.Equal(first.Id,access.CurrentMovement!.Id); Assert.Empty(access.EligibleVehicles);
        Assert.Equal(c.Vehicle.Id,access.CurrentVehicle!.Id); Assert.Null(access.CurrentVehicle.VerificationImage);
        Assert.True((await c.CheckOut.Handle(new(first.Id,c.Vehicle.Id),default)).IsSuccess);
    }
    [Fact] public async Task StaleMovementCannotCloseSubsequentEntryOnSameDay()
    {
        var c = new ParkingTestContext(); var first = (await c.CheckIn.Handle(c.Request,default)).Value;
        c.Clock.UtcNow = c.Clock.UtcNow.AddMinutes(10);
        Assert.True((await c.CheckOut.Handle(new(first.Id,c.Vehicle.Id),default)).IsSuccess);
        c.Clock.UtcNow = c.Clock.UtcNow.AddMinutes(10);
        var expected = Guid.NewGuid();
        var second = (await c.CheckIn.Handle(c.Request with { MovementId=expected },default)).Value;
        Assert.Equal(expected,second.Id);
        Assert.True((await c.CheckOut.Handle(new(first.Id,c.Vehicle.Id),default)).IsFailure);
        Assert.Equal(Domain.Parking.ParkingMovementStatus.OPEN,c.Store.Movements.Single(x=>x.Id==second.Id).Status);
        Assert.True((await c.CheckOut.Handle(new(second.Id,c.Vehicle.Id),default)).IsSuccess);
    }
    [Fact] public async Task MovementAndVehicleMustMatch()
    {
        var c = new ParkingTestContext(); var first = (await c.CheckIn.Handle(c.Request,default)).Value;
        c.Store.Vehicles.Add(new(VehicleType.BICYCLE,null,new Domain.Vehicles.ValueObjects.FrameNumber("OTHER"),"B","M","C",c.Clock.UtcNow));
        Assert.Equal("VEHICLE_NOT_INSIDE",(await c.CheckOut.Handle(new(first.Id,c.Store.Vehicles.Last().Id),default)).Error!.Code);
        Assert.Equal(Domain.Parking.ParkingMovementStatus.OPEN,c.Store.Movements.Single().Status);
    }
    [Fact] public void ExitRequiresBothIdentifiers()
    {
        var validator = new CheckOutVehicleCommandValidator();
        Assert.False(validator.Validate(new CheckOutVehicleCommand(Guid.Empty,Guid.NewGuid())).IsValid);
        Assert.False(validator.Validate(new CheckOutVehicleCommand(Guid.NewGuid(),Guid.Empty)).IsValid);
        Assert.True(validator.Validate(new CheckOutVehicleCommand(Guid.NewGuid(),Guid.NewGuid())).IsValid);
    }
}
