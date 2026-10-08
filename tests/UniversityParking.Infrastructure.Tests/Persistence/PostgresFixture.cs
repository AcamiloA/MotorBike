using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Infrastructure.Tests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("university_parking_tests").WithUsername("test_user")
        .WithPassword("TestContainerOnly1").Build();

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(container.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        // This context always targets this fixture's isolated Testcontainer.
        await context.Database.ExecuteSqlRawAsync("TRUNCATE audit_logs, incident_attachments, incidents, news, parking_movements, parking_zones, parking_lots, vehicle_documents, vehicle_photos, vehicle_registrations, vehicle_ownerships, vehicles, user_roles, user_credentials, users, roles, academic_periods RESTART IDENTITY CASCADE");
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await container.DisposeAsync();
    }
}

[CollectionDefinition("PostgreSQL persistence", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
