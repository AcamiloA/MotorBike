using Microsoft.EntityFrameworkCore;
using Npgsql;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Tests.Persistence;

[Collection("PostgreSQL persistence")]
public sealed class ConstraintTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_ShouldApplyFromEmptyDatabase_AndCreateAllTables()
    {
        await using var context = fixture.CreateContext();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        var tables = await context.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'").ToListAsync();
        Assert.Equal(17, tables.Count);
        Assert.Contains("parking_movements", tables);
        Assert.Contains("vehicle_registrations", tables);
    }

    [Theory]
    [InlineData(true, "ux_users_identification_number")]
    [InlineData(false, "ux_users_card_code")]
    public async Task DuplicateUserIdentifier_ShouldFail(bool identification, string constraint)
    {
        await using var context = fixture.CreateContext();
        context.Users.Add(PersistenceTestData.User("001A", "CARD-1"));
        await context.SaveChangesAsync();
        context.Users.Add(PersistenceTestData.User(identification ? "001A" : "002B", identification ? "CARD-2" : "CARD-1"));
        await AssertUniqueAsync(() => context.SaveChangesAsync(), constraint);
    }

    [Theory]
    [InlineData(VehicleType.CAR, VehicleType.MOTORCYCLE, "ux_vehicles_plate")]
    [InlineData(VehicleType.BICYCLE, VehicleType.BICYCLE, "ux_vehicles_frame_number")]
    public async Task DuplicateNormalizedVehicleIdentifier_ShouldFail(VehicleType first, VehicleType second, string constraint)
    {
        await using var context = fixture.CreateContext();
        context.Vehicles.Add(PersistenceTestData.Vehicle(first, "abc123"));
        await context.SaveChangesAsync();
        context.Vehicles.Add(PersistenceTestData.Vehicle(second, " ABC123 "));
        await AssertUniqueAsync(() => context.SaveChangesAsync(), constraint);
    }

    [Fact]
    public async Task TwoCurrentOwnerships_ShouldFail()
    {
        await using var context = fixture.CreateContext();
        var firstOwner = PersistenceTestData.User();
        var secondOwner = PersistenceTestData.User();
        var vehicle = PersistenceTestData.Vehicle();
        context.AddRange(firstOwner, secondOwner, vehicle);
        context.VehicleOwnerships.Add(new VehicleOwnership(vehicle.Id, firstOwner.Id, PersistenceTestData.Now, firstOwner.Id));
        await context.SaveChangesAsync();
        context.VehicleOwnerships.Add(new VehicleOwnership(vehicle.Id, secondOwner.Id, PersistenceTestData.Now, secondOwner.Id));
        await AssertUniqueAsync(() => context.SaveChangesAsync(), "ux_vehicle_ownerships_current_vehicle");
    }

    [Fact]
    public async Task TwoActiveAcademicPeriods_ShouldFail()
    {
        await using var context = fixture.CreateContext();
        context.AcademicPeriods.Add(PersistenceTestData.Period("2026-2", active: true));
        await context.SaveChangesAsync();
        context.AcademicPeriods.Add(PersistenceTestData.Period("2027-1", active: true));
        await AssertUniqueAsync(() => context.SaveChangesAsync(), "ux_academic_periods_single_active");
    }

    [Theory]
    [InlineData(true, "ux_parking_movements_open_vehicle")]
    [InlineData(false, "ux_parking_movements_open_user")]
    public async Task TwoOpenMovements_ShouldFail(bool sameVehicle, string constraint)
    {
        await using var context = fixture.CreateContext();
        var data = await PersistenceTestData.ParkingAsync(context);
        var secondUser = PersistenceTestData.User();
        var secondVehicle = PersistenceTestData.Vehicle();
        context.AddRange(secondUser, secondVehicle);
        context.ParkingMovements.Add(PersistenceTestData.Movement(data));
        await context.SaveChangesAsync();
        context.ParkingMovements.Add(PersistenceTestData.Movement(data,
            sameVehicle ? secondUser.Id : data.User.Id, sameVehicle ? data.Vehicle.Id : secondVehicle.Id));
        await AssertUniqueAsync(() => context.SaveChangesAsync(), constraint);
    }

    [Fact]
    public async Task InvalidVehicleIdentifierCombination_ShouldFailInDatabase()
    {
        await using var context = fixture.CreateContext();
        var vehicle = PersistenceTestData.Vehicle();
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE vehicles SET frame_number = {"FRAME-X"} WHERE id = {vehicle.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_vehicles_identifier", error.ConstraintName);
    }

    [Fact]
    public async Task InconsistentClosedMovement_ShouldFailInDatabase()
    {
        await using var context = fixture.CreateContext();
        var data = await PersistenceTestData.ParkingAsync(context);
        var movement = PersistenceTestData.Movement(data);
        context.ParkingMovements.Add(movement);
        await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE parking_movements SET status = {"CLOSED"} WHERE id = {movement.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_parking_movements_state", error.ConstraintName);
    }

    [Fact]
    public async Task TransferWithinSamePeriod_ShouldAllowNewOwnerRegistration_AndFutureRenewal()
    {
        await using var context = fixture.CreateContext();
        var firstOwner = PersistenceTestData.User();
        var nextOwner = PersistenceTestData.User();
        var vehicle = PersistenceTestData.Vehicle(VehicleType.CAR);
        var period = PersistenceTestData.Period(active: true);
        var ownership = new VehicleOwnership(vehicle.Id, firstOwner.Id, PersistenceTestData.Now, firstOwner.Id);
        var registration = new VehicleRegistration(vehicle.Id, firstOwner.Id, period.Id, PersistenceTestData.Now);
        context.AddRange(firstOwner, nextOwner, vehicle, period, ownership, registration);
        await context.SaveChangesAsync();
        await using (var transaction = await context.BeginTransactionAsync(CancellationToken.None))
        {
            var transferAt = PersistenceTestData.Now.AddHours(1);
            ownership.Close(transferAt, "Transferencia de prueba");
            registration.Cancel(transferAt, firstOwner.Id, "OWNERSHIP_TRANSFERRED");
            await context.SaveChangesAsync();
            context.VehicleOwnerships.Add(new VehicleOwnership(vehicle.Id, nextOwner.Id, transferAt, firstOwner.Id, "Transferencia de prueba"));
            context.VehicleRegistrations.Add(new VehicleRegistration(vehicle.Id, nextOwner.Id, period.Id, transferAt));
            await context.SaveChangesAsync();
            await transaction.CommitAsync(CancellationToken.None);
        }
        context.ChangeTracker.Clear();
        Assert.Equal(2, await context.VehicleRegistrations.CountAsync());
        Assert.Single(await context.VehicleOwnerships.Where(x => x.EndAt == null).ToListAsync());
        var currentPeriod = await context.AcademicPeriods.SingleAsync();
        currentPeriod.Close();
        var futurePeriod = PersistenceTestData.Period("2027-1", active: true);
        context.AcademicPeriods.Add(futurePeriod);
        await context.SaveChangesAsync();
        context.VehicleRegistrations.Add(new VehicleRegistration(vehicle.Id, nextOwner.Id, futurePeriod.Id, PersistenceTestData.Now.AddDays(1)));
        await context.SaveChangesAsync();
        Assert.Equal(3, await context.VehicleRegistrations.CountAsync());
    }

    [Fact]
    public async Task DuplicateRegistrationTriplet_ShouldFail()
    {
        await using var context = fixture.CreateContext();
        var owner = PersistenceTestData.User();
        var vehicle = PersistenceTestData.Vehicle();
        var period = PersistenceTestData.Period();
        context.AddRange(owner, vehicle, period, new VehicleRegistration(vehicle.Id, owner.Id, period.Id, PersistenceTestData.Now));
        await context.SaveChangesAsync();
        context.VehicleRegistrations.Add(new VehicleRegistration(vehicle.Id, owner.Id, period.Id, PersistenceTestData.Now));
        await AssertUniqueAsync(() => context.SaveChangesAsync(), "ux_vehicle_registrations_vehicle_user_period");
    }

    private static async Task AssertUniqueAsync(Func<Task> save, string expectedConstraint)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(save);
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(expectedConstraint, postgres.ConstraintName);
    }

    [Theory]
    [InlineData(true, "ux_parking_movements_open_vehicle")]
    [InlineData(false, "ux_parking_movements_open_user")]
    public async Task ConcurrentOpenMovements_ShouldPersistExactlyOne(bool sameVehicle, string expectedConstraint)
    {
        await using var setup = fixture.CreateContext();
        var data = await PersistenceTestData.ParkingAsync(setup);
        var otherUser = PersistenceTestData.User();
        var otherVehicle = PersistenceTestData.Vehicle();
        setup.AddRange(otherUser, otherVehicle);
        await setup.SaveChangesAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string?> InsertAsync(Guid userId, Guid vehicleId)
        {
            await using var context = fixture.CreateContext();
            context.ParkingMovements.Add(PersistenceTestData.Movement(data, userId, vehicleId));
            await start.Task;
            try
            {
                await context.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
            {
                return postgres.ConstraintName;
            }
        }

        var first = InsertAsync(data.User.Id, data.Vehicle.Id);
        var second = InsertAsync(sameVehicle ? otherUser.Id : data.User.Id, sameVehicle ? data.Vehicle.Id : otherVehicle.Id);
        start.SetResult();
        var outcomes = await Task.WhenAll(first, second);
        Assert.Single(outcomes, result => result is null);
        Assert.Equal(expectedConstraint, Assert.Single(outcomes, result => result is not null));
        await using var verification = fixture.CreateContext();
        Assert.Equal(1, await verification.ParkingMovements.CountAsync(x => x.Status == ParkingMovementStatus.OPEN));
    }
}
