using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Tests.Persistence;

[Collection("PostgreSQL persistence")]
public sealed class VehicleVerificationPersistenceTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    [Theory][InlineData(VehicleType.CAR)][InlineData(VehicleType.MOTORCYCLE)][InlineData(VehicleType.BICYCLE)]
    public async Task RoundtripAndUniqueVehicle(VehicleType type)
    {
        await using var db = fixture.CreateContext(); var vehicle = PersistenceTestData.Vehicle(type);
        var image = new VehicleVerificationImage(vehicle, "vehicles/evidence.png", "evidence.png", "image/png", 8, PersistenceTestData.Now);
        db.AddRange(vehicle, image); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var read = await db.VehicleVerificationImages.SingleAsync(); Assert.Equal(image.Id, read.Id); Assert.Equal(image.Type, read.Type); Assert.Equal(image.StorageKey, read.StorageKey);
        db.VehicleVerificationImages.Add(new(vehicle, "vehicles/other.png", "other.png", "image/png", 8, PersistenceTestData.Now));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("ux_vehicle_verification_images_vehicle", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }
    [Fact]
    public async Task ForeignKeyRejectsMissingVehicle()
    {
        await using var db = fixture.CreateContext(); var vehicle = PersistenceTestData.Vehicle();
        db.VehicleVerificationImages.Add(new(vehicle, "vehicles/file.png", "file.png", "image/png", 8, PersistenceTestData.Now));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Theory][InlineData("type = 'GENERAL'")][InlineData("size_bytes = 0")][InlineData("content_type = 'application/pdf'")]
    public async Task ConstraintsRejectInvalidRawMetadata(string assignment)
    {
        await using var db = fixture.CreateContext(); var vehicle = PersistenceTestData.Vehicle();
        db.AddRange(vehicle, new VehicleVerificationImage(vehicle, "vehicles/file.png", "file.png", "image/png", 8, PersistenceTestData.Now)); await db.SaveChangesAsync();
        var sql = assignment switch
        {
            "type = 'GENERAL'" => "UPDATE vehicle_verification_images SET type = 'GENERAL'",
            "size_bytes = 0" => "UPDATE vehicle_verification_images SET size_bytes = 0",
            "content_type = 'application/pdf'" => "UPDATE vehicle_verification_images SET content_type = 'application/pdf'",
            _ => throw new ArgumentException("Unknown test case")
        };
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }
    [Fact]
    public async Task MigrationPreservesVehiclesAndLegacyFilesWithoutRelabeling()
    {
        await using var db = fixture.CreateContext(); var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261008214600_AddStudentRegistrationStatuses");
        try
        {
            var vehicle = PersistenceTestData.Vehicle(); var now = PersistenceTestData.Now;
            var photo = new VehiclePhoto(vehicle.Id, VehiclePhotoType.GENERAL, "vehicles/old/photo.png", "old.png", "image/png", 8, now);
            var doc = new VehicleDocument(vehicle.Id, VehicleDocumentType.VEHICLE_REGISTRATION, "vehicles/old/document.pdf", "old.pdf", "application/pdf", 8, now);
            db.AddRange(vehicle, photo, doc); await db.SaveChangesAsync();
            await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.Equal(vehicle.Id, (await db.Vehicles.SingleAsync()).Id);
            Assert.Equal(photo.StorageKey, (await db.VehiclePhotos.SingleAsync()).StorageKey);
            Assert.Equal(doc.StorageKey, (await db.VehicleDocuments.SingleAsync()).StorageKey);
            Assert.Empty(await db.VehicleVerificationImages.ToArrayAsync()); Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await migrator.MigrateAsync(); }
    }
    [Fact]
    public async Task DowngradeDoesNotDeleteNewEvidence()
    {
        await using var db = fixture.CreateContext(); var vehicle = PersistenceTestData.Vehicle();
        db.AddRange(vehicle, new VehicleVerificationImage(vehicle, "vehicles/file.png", "file.png", "image/png", 8, PersistenceTestData.Now)); await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261008214600_AddStudentRegistrationStatuses"));
        Assert.Equal("P0001", error.SqlState); Assert.Single(await db.VehicleVerificationImages.ToArrayAsync());
    }
}
