using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UniversityParking.Domain.Universities;
using UniversityParking.Infrastructure.Persistence.Repositories;

namespace UniversityParking.Infrastructure.Tests.Persistence;

[Collection("PostgreSQL persistence")]
public sealed class UniversityCatalogTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MigrationProvidesExactlyThreeUniversitiesWithoutRunningDemoSeed()
    {
        await using var context = fixture.CreateContext();
        var values = await context.Universities.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        Assert.Equal(new[] { "CMC", "ETITC", "UPN" }, values.Select(x => x.Code));
        Assert.Equal(new[] { UniversityIds.Cmc, UniversityIds.Etitc, UniversityIds.Upn }, values.Select(x => x.Id));
        Assert.Equal(new[] { "Colegio Mayor de Cundinamarca", "ETITC", "U. Pedagógica" }, values.Select(x => x.Name));
        Assert.All(values, x => { Assert.True(x.IsActive); Assert.Equal(x.CreatedAt, x.UpdatedAt); });
        Assert.Empty(await context.Users.ToListAsync());
        Assert.Empty(await context.Roles.ToListAsync());
    }

    [Fact]
    public async Task RepeatedMigrationKeepsReferenceIdentitiesAndExistingUsers()
    {
        await using var context = fixture.CreateContext();
        var user = PersistenceTestData.User(); context.Users.Add(user); await context.SaveChangesAsync();
        await context.Database.MigrateAsync(); await context.Database.MigrateAsync();
        Assert.Equal(3, await context.Universities.CountAsync());
        Assert.Equal(user.Id, (await context.Users.SingleAsync()).Id);
        Assert.Equal(UniversityIds.Etitc, (await context.Users.SingleAsync()).UniversityId);
    }

    [Fact]
    public async Task RepositoryFiltersInactiveButCanReadHistoricalReferenceById()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var university = await context.Universities.SingleAsync(x => x.Id == UniversityIds.Cmc);
        university.Deactivate(university.UpdatedAt.AddDays(1)); await context.SaveChangesAsync();
        var repository = new UniversityRepository(context);
        Assert.Equal(new[] { "ETITC", "UPN" }, (await repository.GetActiveAsync(default)).Select(x => x.Code));
        Assert.False((await repository.GetByIdAsync(UniversityIds.Cmc, default))!.IsActive);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), default));
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task CanonicalCodeIsUniqueInPostgres()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Universities.Add(new University(Guid.NewGuid(), " etitc ", "Otra institución", PersistenceTestData.Now));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_universities_code", postgres.ConstraintName);
        await transaction.RollbackAsync();
    }

    [Theory]
    [InlineData("code", PostgresErrorCodes.NotNullViolation)]
    [InlineData("name", PostgresErrorCodes.NotNullViolation)]
    [InlineData("is_active", PostgresErrorCodes.NotNullViolation)]
    [InlineData("created_at", PostgresErrorCodes.NotNullViolation)]
    [InlineData("updated_at", PostgresErrorCodes.NotNullViolation)]
    public async Task RequiredColumnsRejectNull(string column, string sqlState)
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var sql = column switch
        {
            "code" => "UPDATE universities SET code = NULL WHERE code = 'ETITC'",
            "name" => "UPDATE universities SET name = NULL WHERE code = 'ETITC'",
            "is_active" => "UPDATE universities SET is_active = NULL WHERE code = 'ETITC'",
            "created_at" => "UPDATE universities SET created_at = NULL WHERE code = 'ETITC'",
            "updated_at" => "UPDATE universities SET updated_at = NULL WHERE code = 'ETITC'",
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        };
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(sqlState, error.SqlState);
        await transaction.RollbackAsync();
    }

    [Fact]
    public void SnapshotMatchesModelAndMigrationPreservesUsersDuringBackfill()
    {
        using var context = fixture.CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
        var sql = context.GetService<IMigrator>().GenerateScript("20261007060306_InitialCreate", "20261008140000_AddUniversityCatalog");
        Assert.Contains("CREATE TABLE universities", sql);
        Assert.Contains("ux_universities_code", sql);
        Assert.Contains("Colegio Mayor de Cundinamarca", sql);
        Assert.Contains("university_id", sql);
        Assert.Contains("RAISE EXCEPTION", sql);
        Assert.DoesNotContain("DROP TABLE users", sql);
        Assert.DoesNotContain("DELETE FROM users", sql);
    }

    [Fact]
    public async Task UserUniversityForeignKeyRejectsUnknownReference()
    {
        await using var context = fixture.CreateContext();
        var user = PersistenceTestData.User();
        user.Update(user.FullName, Guid.NewGuid(), user.Career, user.MemberType, user.CardCode, user.UpdatedAt);
        context.Users.Add(user);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task ReferencedUniversityCannotBeDeleted()
    {
        await using var context = fixture.CreateContext();
        context.Users.Add(PersistenceTestData.User()); await context.SaveChangesAsync();
        context.ChangeTracker.Clear(); // Exercise PostgreSQL Restrict, rather than EF's tracked dependent guard.
        context.Universities.Remove(await context.Universities.SingleAsync(x => x.Id == UniversityIds.Etitc));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task UniversityReferenceCannotBeNullInDatabase()
    {
        await using var context = fixture.CreateContext();
        context.Users.Add(PersistenceTestData.User()); await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("UPDATE users SET university_id = NULL"));
        Assert.Equal(PostgresErrorCodes.NotNullViolation, error.SqlState);
    }
}
