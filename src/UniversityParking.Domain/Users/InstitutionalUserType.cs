namespace UniversityParking.Domain.Users;

public enum InstitutionalUserType { STUDENT, TEACHER, ADMINISTRATIVE, GUARD }
public static class InstitutionalUsers
{
    public static string Role(InstitutionalUserType type) => type switch
    {
        InstitutionalUserType.STUDENT or InstitutionalUserType.TEACHER => RoleCodes.User,
        InstitutionalUserType.ADMINISTRATIVE => RoleCodes.Admin,
        InstitutionalUserType.GUARD => RoleCodes.Guard,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
