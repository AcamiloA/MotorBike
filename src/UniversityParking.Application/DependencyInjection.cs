using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Application.Common.Behaviors;

namespace UniversityParking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddLogging();
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
