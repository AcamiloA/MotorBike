using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Application.Common.Behaviors;

namespace UniversityParking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<UniversityParking.Application.Auth.PasswordRecovery.PasswordChallengeService>();
        services.AddOptions<UniversityParking.Application.Documents.DocumentOcrOptions>();
        services.AddSingleton<UniversityParking.Application.Parking.IQrIdentityParser, UniversityParking.Application.Parking.QrIdentityParser>();
        services.AddSingleton<UniversityParking.Application.Documents.ITransitLicenseFormatValidator, UniversityParking.Application.Documents.TransitLicenseFormatValidator>();
        services.AddScoped<UniversityParking.Application.Documents.ITransitLicenseValidationService, UniversityParking.Application.Documents.TransitLicenseValidationService>();
        services.AddOptions<UniversityParking.Application.Universities.Integration.UniversityIntegrationsOptions>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<UniversityParking.Application.Universities.Integration.UniversityIntegrationsOptions>,
            UniversityParking.Application.Universities.Integration.UniversityIntegrationsOptionsValidator>();
        services.AddScoped<UniversityParking.Application.Common.Abstractions.IUniversityStudentValidationService,
            UniversityParking.Application.Universities.Integration.UniversityStudentValidationService>();
        services.AddScoped<UniversityParking.Application.Users.StudentRegistrationReview>();
        services.AddScoped<UniversityParking.Application.Users.AccountProvisioner>();
        services.AddScoped<UniversityParking.Application.Common.Authorization.AdministrationOperationContext>();
        services.AddScoped<UniversityParking.Application.Vehicles.VehicleOperationContext>();
        services.AddSingleton<UniversityParking.Application.Files.FileUploadValidator>();
        services.AddScoped<UniversityParking.Application.Users.UserOperationContext>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        return services;
    }
}
