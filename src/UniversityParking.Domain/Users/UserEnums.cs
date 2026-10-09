namespace UniversityParking.Domain.Users;

public enum MemberType { STUDENT, TEACHER, STAFF }
public enum UserStatus { ACTIVE = 0, INACTIVE = 1, PENDING = 2, REJECTED = 3 }

public static class RoleCodes
{
    public const string User = "USER";
    public const string Guard = "GUARD";
    public const string Admin = "ADMIN";
}
