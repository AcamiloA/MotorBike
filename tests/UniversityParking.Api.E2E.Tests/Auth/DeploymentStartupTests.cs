using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class DeploymentStartupTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task StartupMigrations_CanRunRepeatedly_WithoutChangingMigrationHistory()
    {
        await using var context = fixture.CreateContext();
        var before = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.NotEmpty(before);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var factory = fixture.CreateFactory(settings: new Dictionary<string, string?>
            {
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["HttpsRedirection:Enabled"] = "false"
            });
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(before, (await database.Database.GetAppliedMigrationsAsync()).ToArray());
        }
    }
}
