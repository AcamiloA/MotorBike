using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using UniversityParking.Domain.Universities;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Infrastructure.Tests.Persistence;

public sealed class UniversityLegacyFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("catalog_legacy_tests").WithUsername("test_user").WithPassword("TestContainerOnly1").Build();
    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(container.GetConnectionString()).Options);
    public Task InitializeAsync() => container.StartAsync();
    public async Task DisposeAsync() => await container.DisposeAsync();
}

public sealed class UniversityLegacyMigrationTests(UniversityLegacyFixture fixture) : IClassFixture<UniversityLegacyFixture>, IAsyncLifetime
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid roleId = Guid.NewGuid();
    public async Task InitializeAsync()
    {
        await using var context = fixture.CreateContext();
        // This database belongs exclusively to this class's isolated Testcontainer.
        await context.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
        await context.GetService<IMigrator>().MigrateAsync("20261007060306_InitialCreate");
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task InsertLegacyAsync(string university)
    {
        await using var context = fixture.CreateContext();
        var now = PersistenceTestData.Now;
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users (id, identification_number, full_name, university, career, member_type, card_code, status, created_at, updated_at) VALUES ({userId}, 'LEGACY-001', 'Usuario legacy', {university}, 'Ingeniería', 'STUDENT', 'LEGACY-CARD', 'ACTIVE', {now}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO roles (id, code) VALUES ({roleId}, 'USER')");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO user_roles (user_id, role_id) VALUES ({userId}, {roleId})");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO user_credentials (user_id, password_hash, password_changed_at) VALUES ({userId}, 'PreservedTestHash', {now})");
    }

    [Theory]
    [InlineData("ETITC", "a1100000-0000-4000-8000-000000000001")]
    [InlineData(" etitc ", "a1100000-0000-4000-8000-000000000001")]
    [InlineData("Colegio Mayor de Cundinamarca", "a1100000-0000-4000-8000-000000000002")]
    [InlineData("U. Pedagógica", "a1100000-0000-4000-8000-000000000003")]
    public async Task KnownLegacyPreservesUserCredentialsRolesAndDates(string legacy, string expectedId)
    {
        await InsertLegacyAsync(legacy);
        await using var context = fixture.CreateContext(); await context.Database.MigrateAsync();
        var user = await context.Users.SingleAsync();
        Assert.Equal(userId, user.Id); Assert.Equal(new Guid(expectedId), user.UniversityId);
        Assert.Equal(PersistenceTestData.Now, user.CreatedAt);
        Assert.Equal("PreservedTestHash", (await context.UserCredentials.SingleAsync()).PasswordHash);
        var role = await context.UserRoles.SingleAsync(); Assert.Equal(userId, role.UserId); Assert.Equal(roleId, role.RoleId);
        Assert.Equal(3, await context.Universities.CountAsync());
        Assert.Equal(0L, await context.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM information_schema.columns WHERE table_name = 'users' AND column_name = 'university'").SingleAsync());
        var canonicalName = (await context.Universities.SingleAsync(x => x.Id == user.UniversityId)).Name;
        await context.GetService<IMigrator>().MigrateAsync("20261007060306_InitialCreate");
        Assert.Equal(canonicalName, await context.Database.SqlQueryRaw<string>("SELECT university AS \"Value\" FROM users").SingleAsync());
        Assert.Equal(userId, await context.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM users").SingleAsync());
        Assert.Equal("PreservedTestHash", await context.Database.SqlQueryRaw<string>("SELECT password_hash AS \"Value\" FROM user_credentials").SingleAsync());
        Assert.Equal(roleId, await context.Database.SqlQueryRaw<Guid>("SELECT role_id AS \"Value\" FROM user_roles").SingleAsync());
    }

    [Fact]
    public async Task UnknownLegacyRollsBackCatalogBackfillAndMigrationHistory()
    {
        await InsertLegacyAsync("Universidad desconocida");
        await using var context = fixture.CreateContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());
        Assert.Equal("P0001", error.SqlState);
        Assert.Contains("legacy no reconocida", error.MessageText);
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal("Universidad desconocida", await context.Database.SqlQueryRaw<string>("SELECT university AS \"Value\" FROM users").SingleAsync());
        Assert.Equal(0L, await context.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM information_schema.columns WHERE table_name = 'users' AND column_name = 'university_id'").SingleAsync());
        Assert.True(await context.Database.SqlQueryRaw<bool>("SELECT to_regclass('public.universities') IS NULL AS \"Value\"").SingleAsync());
        Assert.Equal("PreservedTestHash", await context.Database.SqlQueryRaw<string>("SELECT password_hash AS \"Value\" FROM user_credentials").SingleAsync());
        Assert.Equal(roleId, await context.Database.SqlQueryRaw<Guid>("SELECT role_id AS \"Value\" FROM user_roles").SingleAsync());
    }
}
