using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Amazon.S3;
using Amazon.Runtime;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string Provider { get; set; } = "Local";
    public string LocalRootPath { get; set; } = "App_Data/private-files";
    public string? Endpoint { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
    public int SignedUrlExpirationMinutes { get; set; } = 5;
}
public static class StorageRegistration
{
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection("Storage"))
            .Validate(x => x.Provider is "Local" or "S3", "Storage:Provider debe ser Local o S3.")
            .Validate(x => x.SignedUrlExpirationMinutes is >= 1 and <= 15, "La URL firmada debe vencer entre 1 y 15 minutos.")
            .Validate(x => x.Provider != "Local" || !string.IsNullOrWhiteSpace(x.LocalRootPath), "Storage:LocalRootPath es obligatorio.")
            .Validate(x => x.Provider != "S3" || (!string.IsNullOrWhiteSpace(x.Bucket) && !string.IsNullOrWhiteSpace(x.Region) &&
                !string.IsNullOrWhiteSpace(x.AccessKey) && !string.IsNullOrWhiteSpace(x.SecretKey)), "La configuración privada S3 está incompleta.")
            .Validate(x => x.Endpoint is null || Uri.TryCreate(x.Endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http", "Storage:Endpoint no es válido.")
            .ValidateOnStart();
        services.AddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            var config = new AmazonS3Config { AuthenticationRegion = options.Region, ForcePathStyle = options.ForcePathStyle };
            if (options.Endpoint is not null) config.ServiceURL = options.Endpoint;
            else config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        });
        services.AddSingleton<IFileStorage>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            return options.Provider == "Local" ? new LocalFileStorage(options) :
                new S3CompatibleFileStorage(provider.GetRequiredService<IAmazonS3>(), options, provider.GetRequiredService<IClock>());
        });
        return services;
    }
}
