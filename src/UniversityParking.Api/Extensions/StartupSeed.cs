using UniversityParking.Infrastructure.Persistence.Seeding;

namespace UniversityParking.Api.Extensions;

public static class StartupSeed
{
    public static async Task ApplyStartupSeedAsync(this WebApplication app)
    {
        var options = app.Configuration.GetSection("Seed").Get<SeedOptions>() ?? new();
        options.Validate(app.Environment.IsProduction());
        if (!options.Enabled) return;
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeed>().RunAsync(options);
    }
}
