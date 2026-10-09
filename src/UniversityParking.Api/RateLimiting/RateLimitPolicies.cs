namespace UniversityParking.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string Login = "Login";
    public const string StudentRegistration = "StudentRegistration";
    public const int StudentRegistrationPermitLimit = 5;
    public const string PublicCatalog = "PublicCatalog";
    public const string Lookup = "ParkingLookup";
    public const string ParkingCommands = "ParkingCommands";
    public const int LoginPermitLimit = 5;
    public const int LookupPermitLimit = 60;
    public const int ParkingCommandsPermitLimit = 30;
    public const int GeneralPermitLimit = 120;
}
