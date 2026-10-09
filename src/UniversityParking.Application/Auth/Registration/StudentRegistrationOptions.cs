namespace UniversityParking.Application.Auth.Registration;

public sealed class StudentRegistrationOptions
{
    public const string SectionName = "StudentRegistration";
    public bool AutoApprove { get; set; } = false;
}
