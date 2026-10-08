using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Infrastructure.Storage;

namespace UniversityParking.Infrastructure.Tests.Storage;

public sealed class StorageConfigurationTests
{
    [Theory]
    [InlineData("Unsupported", "5")]
    [InlineData("S3", "5")]
    [InlineData("Local", "1440")]
    public void InvalidProviderMissingS3SecretsOrLongLivedUrlsAreRejected(string provider, string expiration)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Storage:Provider"] = provider, ["Storage:SignedUrlExpirationMinutes"] = expiration }).Build();
        using var services = new ServiceCollection().AddFileStorage(configuration).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<StorageOptions>>().Value);
    }
}
