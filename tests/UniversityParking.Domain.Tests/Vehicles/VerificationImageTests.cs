using UniversityParking.Domain.Common;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Domain.Tests.Vehicles;

public sealed class VerificationImageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static Vehicle Vehicle(VehicleType type) => new(type, type == VehicleType.BICYCLE ? null : new("ABC123"),
        type == VehicleType.BICYCLE ? new("FRAME01") : null, "Marca", "Modelo", "Color", Now);
    [Theory]
    [InlineData(VehicleType.CAR, VehicleVerificationImageType.TRANSIT_LICENSE_FRONT)]
    [InlineData(VehicleType.MOTORCYCLE, VehicleVerificationImageType.TRANSIT_LICENSE_FRONT)]
    [InlineData(VehicleType.BICYCLE, VehicleVerificationImageType.BICYCLE_PHOTO)]
    public void TypeIsDerivedAndMetadataPreserved(VehicleType type, VehicleVerificationImageType expected)
    {
        var vehicle = Vehicle(type); var image = new VehicleVerificationImage(vehicle, "vehicles/image.png", "image.png", "image/png", 8, Now);
        Assert.Equal(expected, image.Type); Assert.Equal(vehicle.Id, image.VehicleId); Assert.Equal(Now, image.CreatedAt); Assert.Equal(Now, image.UpdatedAt);
        Assert.Equal(8, image.SizeBytes); image.EnsureCompatible(type);
        Assert.Throws<DomainException>(() => image.EnsureCompatible(type == VehicleType.BICYCLE ? VehicleType.CAR : VehicleType.BICYCLE));
    }
    [Theory][InlineData("application/pdf", 8)][InlineData("image/heic", 8)][InlineData("image/png", 0)][InlineData("image/png", 5242881)]
    public void InvalidMetadataIsRejected(string mime, long size)
        => Assert.Throws<DomainException>(() => new VehicleVerificationImage(Vehicle(VehicleType.CAR), "vehicles/file.png", "file.png", mime, size, Now));
    [Theory][InlineData("image/jpeg")][InlineData("image/png")]
    public void ReplacementPreservesIdentityAndCreation(string mime)
    {
        var vehicle = Vehicle(VehicleType.CAR); var image = new VehicleVerificationImage(vehicle, "vehicles/old.png", "old.png", "image/png", 8, Now);
        var id = image.Id; image.Replace(vehicle, "vehicles/new.jpg", "new.jpg", mime, 100, Now.AddMinutes(1));
        Assert.Equal(id, image.Id); Assert.Equal(Now, image.CreatedAt); Assert.Equal(Now.AddMinutes(1), image.UpdatedAt);
        Assert.Equal("vehicles/new.jpg", image.StorageKey); Assert.Equal(mime, image.ContentType);
    }
    [Fact]
    public void FailedReplacementDoesNotMutateMetadata()
    {
        var vehicle = Vehicle(VehicleType.BICYCLE); var image = new VehicleVerificationImage(vehicle, "vehicles/old.png", "old.png", "image/png", 8, Now);
        Assert.Throws<DomainException>(() => image.Replace(vehicle, "vehicles/new.pdf", "new.pdf", "application/pdf", 8, Now));
        Assert.Equal("vehicles/old.png", image.StorageKey);
        Assert.Throws<DomainException>(() => image.Replace(Vehicle(VehicleType.BICYCLE), "vehicles/new.png", "new.png", "image/png", 8, Now));
        Assert.Throws<DomainException>(() => image.Replace(vehicle, "vehicles/new.png", "new.png", "image/png", 8, Now.AddMinutes(-1)));
    }
}
