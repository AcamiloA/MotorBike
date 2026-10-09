using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Infrastructure.Documents;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Tests.Documents;

public sealed class TextractOptionsTests
{
    [Theory][InlineData("unknown", 15)][InlineData("us-east-1", 0)][InlineData("us-east-1", 61)]
    public void InvalidRegionOrTimeoutFailsConfiguration(string region, int seconds)
    {
        var services = new ServiceCollection(); services.AddDocumentOcr(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Textract:Region"] = region, ["Textract:TimeoutSeconds"] = seconds.ToString() }).Build());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TextractOptions>>().Value);
    }
    [Fact]
    public void ResolvingExtractorDoesNotCreateSdkClientOrRequireStorageCredentials()
    {
        var services = new ServiceCollection(); services.AddDocumentOcr(new ConfigurationBuilder().Build());
        var calls = 0; services.AddSingleton<Func<Amazon.Textract.IAmazonTextract>>(_ => () => { calls++; throw new InvalidOperationException("No SDK calls allowed"); });
        using var provider = services.BuildServiceProvider(); Assert.IsType<AwsTextractDocumentTextExtractor>(provider.GetRequiredService<IDocumentTextExtractor>());
        var options = provider.GetRequiredService<IOptions<TextractOptions>>().Value;
        Assert.Equal("us-east-1", options.Region); Assert.Equal(15, options.TimeoutSeconds); Assert.Equal(0, calls);
    }
}
