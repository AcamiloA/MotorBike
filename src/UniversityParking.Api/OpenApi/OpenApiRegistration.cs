using Microsoft.OpenApi;

namespace UniversityParking.Api.OpenApi;

public static class OpenApiRegistration
{
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "MOTOBIKE PARK API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Ingresa el AccessToken de la sesión."
            });
            options.OperationFilter<AuthorizationOperationFilter>();
            options.OperationFilter<VehicleFileOperationFilter>();
            options.OperationFilter<ParkingOperationFilter>();
            options.OperationFilter<IncidentOperationFilter>();
        });
        return services;
    }
}


