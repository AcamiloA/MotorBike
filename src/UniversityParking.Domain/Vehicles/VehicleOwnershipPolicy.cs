using UniversityParking.Domain.Users;

namespace UniversityParking.Domain.Vehicles;

public static class VehicleOwnershipPolicy
{
    public static bool CanOwn(MemberType memberType, VehicleType vehicleType) =>
        Enum.IsDefined(memberType) && Enum.IsDefined(vehicleType) &&
        (memberType != MemberType.STUDENT || vehicleType != VehicleType.CAR);
}
