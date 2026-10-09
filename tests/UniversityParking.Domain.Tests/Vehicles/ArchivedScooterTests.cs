using UniversityParking.Domain.Common;
using UniversityParking.Domain.Vehicles;
namespace UniversityParking.Domain.Tests.Vehicles;
public sealed class ArchivedScooterTests
{
    [Fact] public void ScooterUsesSerialPhotoAndUppercase()
    {
        var now=DateTimeOffset.UtcNow;var vehicle=new Vehicle(VehicleType.SCOOTER,null,new(" serial123 ")," xiaomi "," s1 ","negro",now);
        Assert.Null(vehicle.Plate);Assert.Equal("SERIAL123",vehicle.FrameNumber!.Value);Assert.Equal("XIAOMI",vehicle.Brand);Assert.Equal("S1",vehicle.Model);Assert.Equal("NEGRO",vehicle.Color);
        Assert.Equal(VehicleVerificationImageType.SCOOTER_PHOTO,VehicleVerificationImage.ForVehicle(vehicle.Type));
    }
    [Fact] public void ArchivePreservesIdentityButBlocksMutation()
    {
        var now=DateTimeOffset.UtcNow;var vehicle=new Vehicle(VehicleType.SCOOTER,null,new("SERIAL"),"brand","model","color",now);
        var id=vehicle.Id;vehicle.Archive(now.AddMinutes(1));Assert.Equal(id,vehicle.Id);Assert.Equal(VehicleStatus.INACTIVE,vehicle.Status);Assert.NotNull(vehicle.DeletedAt);
        Assert.Throws<DomainException>(()=>vehicle.Activate(now.AddMinutes(2)));Assert.Throws<DomainException>(()=>vehicle.UpdateDescription("new","new","new",now.AddMinutes(2)));
        Assert.Throws<DomainException>(()=>vehicle.CorrectIdentifier("OTHER",now.AddMinutes(2)));
    }
    [Fact] public void UpdateDescriptionNormalizesUppercase()
    {
        var now=DateTimeOffset.UtcNow;var vehicle=new Vehicle(VehicleType.BICYCLE,null,new("SERIAL"),"b","m","c",now);
        vehicle.UpdateDescription(" yamaha ","model","negro",now);Assert.Equal("YAMAHA",vehicle.Brand);Assert.Equal("MODEL",vehicle.Model);Assert.Equal("NEGRO",vehicle.Color);
    }
}
