namespace UniversityParking.Api.Authorization;

public static class PolicyNames
{
    public const string Authenticated = "Authenticated";
    public const string Guard = "Guard";
    public const string Admin = "Admin";
    public const string GuardOrAdmin = "GuardOrAdmin";
}
