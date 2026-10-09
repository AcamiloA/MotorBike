using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Application.Universities.Integration;

// Resolves technical validation only. Admission policy belongs to the future use case.
public sealed class UniversityStudentValidationService : IUniversityStudentValidationService
{
    private readonly IUniversityRepository universities;
    private readonly UniversityIntegrationsOptions options;
    private readonly IReadOnlyDictionary<Guid, IUniversityStudentValidator> providers;

    public UniversityStudentValidationService(IUniversityRepository universities,
        IOptions<UniversityIntegrationsOptions> options, IEnumerable<IUniversityStudentValidator> providers)
    {
        this.universities = universities;
        this.options = options.Value;
        var adapters = providers.ToArray();
        var validation = new UniversityIntegrationsOptionsValidator(adapters).Validate(null, this.options);
        if (validation.Failed)
            throw new OptionsValidationException(UniversityIntegrationsOptions.SectionName,
                typeof(UniversityIntegrationsOptions), validation.Failures);
        this.providers = adapters.ToDictionary(x => x.UniversityId);
    }

    public async Task ValidateConfigurationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var setting in options.Universities)
        {
            if (await universities.GetByIdAsync(setting.UniversityId, cancellationToken) is null)
                throw new OptionsValidationException(UniversityIntegrationsOptions.SectionName,
                    typeof(UniversityIntegrationsOptions), ["UniversityIntegrations: referencia fuera del catálogo."]);
        }
    }

    public async Task<UniversityStudentValidationResult> ValidateAsync(
        Guid universityId, string identificationNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Universities.Any(x => x.UniversityId == universityId && x.Enabled))
            return new(UniversityStudentValidationStatus.NOT_CONFIGURED);
        var university = await universities.GetByIdAsync(universityId, cancellationToken);
        if (university is null)
            throw new OptionsValidationException(UniversityIntegrationsOptions.SectionName,
                typeof(UniversityIntegrationsOptions), ["UniversityIntegrations: referencia fuera del catálogo."]);
        cancellationToken.ThrowIfCancellationRequested();
        UniversityStudentValidationResult result;
        try
        {
            result = await providers[university.Id].ValidateAsync(
                new(university.Id, university.Code, identificationNumber), cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UniversityStudentValidationStatus.UNAVAILABLE);
        }
        catch (TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(UniversityStudentValidationStatus.UNAVAILABLE);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UniversityStudentValidationStatus.ERROR);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result is not null && Enum.IsDefined(result.Status)
            ? result : new(UniversityStudentValidationStatus.ERROR);
    }
}
