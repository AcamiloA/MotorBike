using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Infrastructure.Persistence.Repositories;
using UniversityParking.Infrastructure.Time;
using UniversityParking.Infrastructure.Storage;
using UniversityParking.Infrastructure.Authentication;

namespace UniversityParking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("La configuración ConnectionStrings:DefaultConnection es obligatoria.");

        services.AddOptions<UniversityParking.Application.Auth.Registration.StudentRegistrationOptions>()
            .Bind(configuration.GetSection(UniversityParking.Application.Auth.Registration.StudentRegistrationOptions.SectionName)).ValidateOnStart();
        services.AddOptions<UniversityParking.Application.Universities.Integration.UniversityIntegrationsOptions>()
            .Bind(configuration.GetSection(UniversityParking.Application.Universities.Integration.UniversityIntegrationsOptions.SectionName),
                options => options.ErrorOnUnknownConfiguration = true).ValidateOnStart();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        UniversityParking.Infrastructure.Documents.TextractRegistration.AddDocumentOcr(services, configuration);
        services.AddScoped<UniversityParking.Infrastructure.Persistence.Seeding.DemoSeed>();
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(UserPersistenceBehavior<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(VehicleConstraintBehavior<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(AdministrationConstraintBehavior<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ParkingConstraintBehavior<,>));
        services.AddScoped<IParkingMovementReadRepository, ParkingMovementReadRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddOptions<ParkingTimeOptions>().Bind(configuration.GetSection("Parking"))
            .Validate(x => x.TimeZone == "America/Bogota", "Parking:TimeZone debe ser America/Bogota.").ValidateOnStart();
        services.AddSingleton<IParkingTimeZone, BogotaParkingTimeZone>();
        services.AddScoped<IVehicleEvidenceRepository, VehicleEvidenceRepository>();
        services.AddFileStorage(configuration);
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUniversityRepository, UniversityRepository>();
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleOwnershipRepository, VehicleOwnershipRepository>();
        services.AddScoped<IVehicleRegistrationRepository, VehicleRegistrationRepository>();
        services.AddScoped<IAcademicPeriodRepository, AcademicPeriodRepository>();
        services.AddScoped<IParkingLotRepository, ParkingLotRepository>();
        services.AddScoped<IParkingMovementRepository, ParkingMovementRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<INewsRepository, NewsRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddJwtOptions(configuration);
        return services;
    }
}
