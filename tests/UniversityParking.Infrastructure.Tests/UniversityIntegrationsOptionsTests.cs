using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Application;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Universities.Integration;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Infrastructure.Tests;

public sealed class UniversityIntegrationsOptionsTests
{
    private static ServiceProvider Provider(Dictionary<string, string?> values)
    {
        values["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=options_test;Username=test";
        var services = new ServiceCollection(); services.AddApplication();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void DefaultIsEmptyWithoutAdapters()
    {
        using var provider = Provider([]);
        Assert.Empty(provider.GetRequiredService<IOptions<UniversityIntegrationsOptions>>().Value.Universities);
        Assert.Empty(provider.GetServices<IUniversityStudentValidator>());
        using var scope = provider.CreateScope();
        Assert.IsType<UniversityStudentValidationService>(scope.ServiceProvider.GetRequiredService<IUniversityStudentValidationService>());
    }

    [Fact]
    public void IdentityBindsAndMissingEnabledDefaultsFalse()
    {
        using var provider = Provider(new() { ["UniversityIntegrations:Universities:0:UniversityId"] = UniversityIds.Etitc.ToString() });
        var item = Assert.Single(provider.GetRequiredService<IOptions<UniversityIntegrationsOptions>>().Value.Universities);
        Assert.Equal(UniversityIds.Etitc, item.UniversityId); Assert.False(item.Enabled);
    }

    [Theory][InlineData("true")][InlineData("invalid")]
    public void EnabledWithoutAdapterOrInvalidBooleanCannotResolve(string enabled)
    {
        using var provider = Provider(new() { ["UniversityIntegrations:Universities:0:UniversityId"] = UniversityIds.Etitc.ToString(),
            ["UniversityIntegrations:Universities:0:Enabled"] = enabled });
        var exception = Record.Exception(() => provider.GetRequiredService<IOptions<UniversityIntegrationsOptions>>().Value);
        Assert.NotNull(exception);
        if (enabled == "true") Assert.IsType<OptionsValidationException>(exception);
        else Assert.IsType<InvalidOperationException>(exception);
    }

    [Theory][InlineData("")][InlineData("not-a-guid")][InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidIdentityIsRejected(string id)
    {
        using var provider = Provider(new() { ["UniversityIntegrations:Universities:0:UniversityId"] = id });
        Assert.NotNull(Record.Exception(() => provider.GetRequiredService<IOptions<UniversityIntegrationsOptions>>().Value));
    }

    [Fact]
    public void UnknownConfigurationPropertyIsRejected()
    {
        using var provider = Provider(new() { ["UniversityIntegrations:ApiToken"] = "test-only-marker" });
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<UniversityIntegrationsOptions>>().Value);
    }

    [Fact]
    public void EnvironmentDoubleUnderscoreBindsWithoutSecrets()
    {
        var prefix = "MOTORBIKE_INTEGRATIONS_TEST_" + Guid.NewGuid().ToString("N") + "_";
        var key = prefix + "UniversityIntegrations__Universities__0__UniversityId";
        try
        {
            Environment.SetEnvironmentVariable(key, UniversityIds.Cmc.ToString());
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            var services = new ServiceCollection(); services.AddApplication();
            services.AddOptions<UniversityIntegrationsOptions>().Bind(configuration.GetSection(UniversityIntegrationsOptions.SectionName));
            using var provider = services.BuildServiceProvider();
            var entry = Assert.Single(provider.GetRequiredService<IOptions<UniversityIntegrationsOptions>>().Value.Universities);
            Assert.Equal(UniversityIds.Cmc, entry.UniversityId); Assert.False(entry.Enabled);
        }
        finally { Environment.SetEnvironmentVariable(key, null); }
    }
}
