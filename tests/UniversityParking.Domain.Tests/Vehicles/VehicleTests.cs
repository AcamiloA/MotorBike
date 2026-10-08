using UniversityParking.Domain.Common;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Domain.Tests.Vehicles;

public sealed class VehicleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(MemberType.STUDENT, VehicleType.CAR, false)]
    [InlineData(MemberType.STUDENT, VehicleType.MOTORCYCLE, true)]
    [InlineData(MemberType.STUDENT, VehicleType.BICYCLE, true)]
    [InlineData(MemberType.TEACHER, VehicleType.CAR, true)]
    [InlineData(MemberType.TEACHER, VehicleType.MOTORCYCLE, true)]
    [InlineData(MemberType.TEACHER, VehicleType.BICYCLE, true)]
    [InlineData(MemberType.STAFF, VehicleType.CAR, true)]
    [InlineData(MemberType.STAFF, VehicleType.MOTORCYCLE, true)]
    [InlineData(MemberType.STAFF, VehicleType.BICYCLE, true)]
    public void OwnershipPolicy_ShouldEnforceMemberVehicleMatrix(MemberType member, VehicleType vehicle, bool allowed) =>
        Assert.Equal(allowed, VehicleOwnershipPolicy.CanOwn(member, vehicle));

    [Fact]
    public void OwnershipPolicy_ShouldRejectUndefinedEnums()
    {
        Assert.False(VehicleOwnershipPolicy.CanOwn((MemberType)99, VehicleType.CAR));
        Assert.False(VehicleOwnershipPolicy.CanOwn(MemberType.STAFF, (VehicleType)99));
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData(" ABC-123 ")]
    [InlineData("abc 123")]
    [InlineData("a\tb c-123")]
    public void Plate_ShouldNormalizeAndCompareByValue(string input)
    {
        var plate = new VehiclePlate(input);
        Assert.Equal("ABC123", plate.Value);
        Assert.Equal(new VehiclePlate("ABC123"), plate);
    }

    [Fact]
    public void FrameNumber_ShouldNormalizeWhitespaceAndCase_WithoutRemovingHyphens()
    {
        var frame = new FrameNumber(" ab - 012 ");
        Assert.Equal("AB-012", frame.Value);
        Assert.Equal(new FrameNumber("AB-012"), frame);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void VehicleIdentifiers_ShouldRejectEmptyValues(string input)
    {
        Assert.Throws<DomainException>(() => new VehiclePlate(input));
        Assert.Throws<DomainException>(() => new FrameNumber(input));
    }

    [Theory]
    [InlineData(VehicleType.CAR, false, false)]
    [InlineData(VehicleType.CAR, true, true)]
    [InlineData(VehicleType.CAR, false, true)]
    [InlineData(VehicleType.MOTORCYCLE, false, false)]
    [InlineData(VehicleType.MOTORCYCLE, true, true)]
    [InlineData(VehicleType.MOTORCYCLE, false, true)]
    [InlineData(VehicleType.BICYCLE, true, false)]
    [InlineData(VehicleType.BICYCLE, true, true)]
    [InlineData(VehicleType.BICYCLE, false, false)]
    public void Vehicle_ShouldRejectInvalidIdentifierCombinations(VehicleType type, bool plate, bool frame) =>
        Assert.Throws<DomainException>(() => new Vehicle(type, plate ? new VehiclePlate("ABC123") : null,
            frame ? new FrameNumber("FRAME1") : null, "Marca", "Modelo", "Negro", Now));

    [Theory]
    [InlineData(VehicleType.CAR)]
    [InlineData(VehicleType.MOTORCYCLE)]
    [InlineData(VehicleType.BICYCLE)]
    public void Vehicle_ShouldAcceptCorrectIdentifierAndStartActive(VehicleType type)
    {
        var vehicle = Create(type);
        Assert.Equal(VehicleStatus.ACTIVE, vehicle.Status);
        Assert.NotEqual(Guid.Empty, vehicle.Id);
        Assert.Equal(type == VehicleType.BICYCLE, vehicle.Plate is null);
        Assert.Equal(type != VehicleType.BICYCLE, vehicle.FrameNumber is null);
    }

    [Fact]
    public void UpdateDescription_ShouldNotAlterIdentityOrType()
    {
        var vehicle = Create(VehicleType.MOTORCYCLE);
        var id = vehicle.Id;
        vehicle.UpdateDescription(" Nueva marca ", " Nuevo modelo ", " Azul ", Now.AddMinutes(1));
        Assert.Equal("Nueva marca", vehicle.Brand);
        Assert.Equal("Nuevo modelo", vehicle.Model);
        Assert.Equal("Azul", vehicle.Color);
        Assert.Equal("ABC123", vehicle.Plate!.Value);
        Assert.Equal(VehicleType.MOTORCYCLE, vehicle.Type);
        Assert.Equal(id, vehicle.Id);
        Assert.Throws<DomainException>(() => vehicle.UpdateDescription("Otra", " ", "Rojo", Now.AddMinutes(2)));
        Assert.Equal("Nueva marca", vehicle.Brand);
    }

    [Theory]
    [InlineData(VehicleType.CAR)]
    [InlineData(VehicleType.BICYCLE)]
    public void CorrectIdentifier_ShouldPreserveVehicleIdAndIdentifierConsistency(VehicleType type)
    {
        var vehicle = Create(type);
        var id = vehicle.Id;
        vehicle.CorrectIdentifier(" xy- 12 ", Now.AddMinutes(1));
        Assert.Equal(id, vehicle.Id);
        Assert.Equal(type, vehicle.Type);
        if (type == VehicleType.BICYCLE)
        {
            Assert.Equal("XY-12", vehicle.FrameNumber!.Value);
            Assert.Null(vehicle.Plate);
        }
        else
        {
            Assert.Equal("XY12", vehicle.Plate!.Value);
            Assert.Null(vehicle.FrameNumber);
        }
    }

    [Fact]
    public void VehicleStatus_ShouldBeIdempotent()
    {
        var vehicle = Create(VehicleType.CAR);
        vehicle.Deactivate(Now.AddMinutes(1));
        vehicle.Deactivate(Now.AddMinutes(2));
        Assert.Equal(VehicleStatus.INACTIVE, vehicle.Status);
        Assert.Equal(Now.AddMinutes(1), vehicle.UpdatedAt);
        vehicle.Activate(Now.AddMinutes(3));
        vehicle.Activate(Now.AddMinutes(4));
        Assert.Equal(VehicleStatus.ACTIVE, vehicle.Status);
        Assert.Equal(Now.AddMinutes(3), vehicle.UpdatedAt);
    }

    [Theory]
    [InlineData(VehicleType.CAR, VehicleDocumentType.VEHICLE_REGISTRATION, VehicleDocumentType.INSURANCE)]
    [InlineData(VehicleType.MOTORCYCLE, VehicleDocumentType.VEHICLE_REGISTRATION, VehicleDocumentType.INSURANCE)]
    public void DocumentPolicy_ShouldRequireRegistrationAndInsurance(VehicleType type, VehicleDocumentType first, VehicleDocumentType second)
    {
        Assert.Equal(new[] { first, second }, VehicleDocumentPolicy.GetRequiredDocuments(type));
        Assert.True(VehicleDocumentPolicy.HasRequiredDocuments(type, new[] { first, second }));
        Assert.False(VehicleDocumentPolicy.HasRequiredDocuments(type, new[] { first, first }));
    }

    [Fact]
    public void BicycleDocumentPolicy_ShouldRequireOnlyOwnershipSupport()
    {
        Assert.Equal(new[] { VehicleDocumentType.OWNERSHIP_SUPPORT }, VehicleDocumentPolicy.GetRequiredDocuments(VehicleType.BICYCLE));
        Assert.True(VehicleDocumentPolicy.HasRequiredDocuments(VehicleType.BICYCLE, new[] { VehicleDocumentType.OWNERSHIP_SUPPORT }));
        Assert.False(VehicleDocumentPolicy.HasRequiredDocuments(VehicleType.BICYCLE, Array.Empty<VehicleDocumentType>()));
    }

    private static Vehicle Create(VehicleType type) => new(type,
        type == VehicleType.BICYCLE ? null : new VehiclePlate("abc-123"),
        type == VehicleType.BICYCLE ? new FrameNumber("FRAME1") : null, "Marca", "Modelo", "Negro", Now);
}
