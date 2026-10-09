using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Application.Universities.Integration;

public sealed class UniversityIntegrationsOptionsValidator(IEnumerable<IUniversityStudentValidator> providers)
    : IValidateOptions<UniversityIntegrationsOptions>
{
    public ValidateOptionsResult Validate(string? name, UniversityIntegrationsOptions options)
    {
        var adapters = providers.ToArray();
        if (adapters.Any(x => x.UniversityId == Guid.Empty) ||
            adapters.GroupBy(x => x.UniversityId).Any(x => x.Count() > 1))
            return ValidateOptionsResult.Fail("UniversityIntegrations: proveedor sin identidad o duplicado.");
        if (options.Universities is null || options.Universities.Any(x => x is null || x.UniversityId == Guid.Empty) ||
            options.Universities.GroupBy(x => x.UniversityId).Any(x => x.Count() > 1))
            return ValidateOptionsResult.Fail("UniversityIntegrations: referencias de universidad inválidas o duplicadas.");
        if (options.Universities.Any(x => x.Enabled && !adapters.Any(p => p.UniversityId == x.UniversityId)))
            return ValidateOptionsResult.Fail("UniversityIntegrations: integración habilitada sin proveedor registrado.");
        return ValidateOptionsResult.Success;
    }
}
