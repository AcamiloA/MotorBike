namespace UniversityParking.Domain.Users;

public enum MemberType { STUDENT, TEACHER, STAFF }
public enum UserStatus { ACTIVE, INACTIVE }

public static class RoleCodes
{
    public const string User = "USER";
    public const string Guard = "GUARD";
    public const string Admin = "ADMIN";
}
