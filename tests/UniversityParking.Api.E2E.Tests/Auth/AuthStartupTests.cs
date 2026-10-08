using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UniversityParking.Api.E2E.Tests.Auth;

public sealed class AuthStartupTests
{
    [Fact]
    public void ApiStartup_ShouldRejectMissingJwtKeyBeforeAcceptingRequests()
    {
        using var factory = new MissingKeyFactory();
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(error.Failures, failure => failure.Contains("Jwt:Key"));
    }

    private sealed class MissingKeyFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=startup_validation_only;Username=test_user",
                ["Jwt:Key"] = ""
            }));
            return base.CreateHost(builder);
        }
    }
}
