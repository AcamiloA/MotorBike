using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Infrastructure.Time;

namespace UniversityParking.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void MissingConnectionString_ShouldFailClearlyWithoutExposingSecrets()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(config));
        Assert.Contains("ConnectionStrings:DefaultConnection", error.Message);
    }

    [Fact]
    public void ContextAndUnitOfWork_ShouldShareScopedInstance_AndDifferAcrossScopes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=model_test;Username=model_test"
        }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Same(firstContext, first.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotSame(firstContext, second.ServiceProvider.GetRequiredService<AppDbContext>());
        Assert.IsType<SystemClock>(first.ServiceProvider.GetRequiredService<IClock>());
        Assert.Equal(TimeSpan.Zero, first.ServiceProvider.GetRequiredService<IClock>().UtcNow.Offset);
    }
}
