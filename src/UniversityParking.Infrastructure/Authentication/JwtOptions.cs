using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UniversityParking.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "UniversityParking.Api";
    public string Audience { get; set; } = "UniversityParking.Mobile";
    public int ExpirationMinutes { get; set; } = 480;
}

public static class JwtOptionsRegistration
{
    public static IServiceCollection AddJwtOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Key) && Encoding.UTF8.GetByteCount(options.Key) >= 32, "Jwt:Key debe contener al menos 32 bytes.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer es obligatorio.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience es obligatorio.")
            .Validate(options => options.ExpirationMinutes == 480, "Jwt:ExpirationMinutes debe ser 480.")
            .ValidateOnStart();
        return services;
    }
}
