using Microsoft.EntityFrameworkCore;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.Extensions;

public static class StartupMigrations
{
    public static async Task ApplyStartupMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup")) return;
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // EF migrations take the provider's migration lock. Failure prevents readiness.
        await database.Database.MigrateAsync();
        app.Logger.LogInformation("Migraciones de base de datos aplicadas.");
    }
}
