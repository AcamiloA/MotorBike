using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Api.Extensions;

public static class StartupUniversityIntegrations
{
    public static async Task ValidateUniversityIntegrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUniversityStudentValidationService>()
            .ValidateConfigurationAsync(app.Lifetime.ApplicationStopping);
    }
}
