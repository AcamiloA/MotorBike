namespace UniversityParking.Application.Common.Abstractions;

// A future institutional adapter; never registered with a test double in production.
public interface IUniversityStudentValidator
{
    Guid UniversityId { get; }
    Task<UniversityStudentValidationResult> ValidateAsync(
        UniversityStudentValidationRequest request, CancellationToken cancellationToken);
}

public sealed record UniversityStudentValidationRequest(
    Guid UniversityId, string UniversityCode, string IdentificationNumber)
{
    public override string ToString() => $"UniversityStudentValidationRequest {{ UniversityId = {UniversityId} }}";
}

public enum UniversityStudentValidationStatus
{
    VALID, NOT_FOUND, UNAVAILABLE, NOT_CONFIGURED, ERROR
}

// Intentionally excludes institutional payloads, personal data and technical messages.
public sealed record UniversityStudentValidationResult(UniversityStudentValidationStatus Status);

public interface IUniversityStudentValidationService
{
    Task<UniversityStudentValidationResult> ValidateAsync(
        Guid universityId, string identificationNumber, CancellationToken cancellationToken);
    Task ValidateConfigurationAsync(CancellationToken cancellationToken);
}
