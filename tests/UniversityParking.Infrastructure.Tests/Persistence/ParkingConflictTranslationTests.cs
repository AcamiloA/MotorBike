using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Parking;
using UniversityParking.Domain.Parking;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Infrastructure.Tests.Persistence;

[Collection("PostgreSQL persistence")]
public sealed class ParkingConflictTranslationTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    [Theory]
    [InlineData(true, "VEHICLE_ALREADY_INSIDE")]
    [InlineData(false, "USER_ALREADY_HAS_VEHICLE_INSIDE")]
    public async Task ActualPostgresUniqueViolationMapsToFunctionalError(bool sameVehicle, string expectedCode)
    {
        await using var context = fixture.CreateContext();
        var data = await PersistenceTestData.ParkingAsync(context);
        var secondUser = PersistenceTestData.User();
        var secondVehicle = PersistenceTestData.Vehicle();
        context.AddRange(secondUser, secondVehicle);
        context.ParkingMovements.Add(PersistenceTestData.Movement(data));
        await context.SaveChangesAsync();
        var userId = sameVehicle ? secondUser.Id : data.User.Id;
        var vehicleId = sameVehicle ? data.Vehicle.Id : secondVehicle.Id;
        var behavior = new ParkingConstraintBehavior<CheckInVehicleCommand, Result<ParkingMovementView>>(context);
        var result = await behavior.Handle(new(userId, vehicleId, data.Lot.Id), async cancellationToken =>
        {
            context.ParkingMovements.Add(new ParkingMovement(userId, vehicleId, data.Lot.Id, data.Zone.Id, PersistenceTestData.Now, data.Guard.Id));
            await context.SaveChangesAsync(cancellationToken);
            return Result<ParkingMovementView>.Failure(new("TEST_UNEXPECTED", "Unexpected successful insert.", ErrorType.Unexpected));
        }, default);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await using var verification = fixture.CreateContext();
        Assert.Single(await verification.ParkingMovements.ToArrayAsync());
    }
    [Fact]
    public async Task ReusedMovementIdentityMapsActualPrimaryKeyConflict()
    {
        await using var context = fixture.CreateContext();
        var data = await PersistenceTestData.ParkingAsync(context);
        var original = PersistenceTestData.Movement(data);
        original.Close(PersistenceTestData.Now.AddMinutes(1), data.Guard.Id);
        context.ParkingMovements.Add(original);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var behavior = new ParkingConstraintBehavior<CheckInVehicleCommand, Result<ParkingMovementView>>(context);
        var result = await behavior.Handle(new(data.User.Id, data.Vehicle.Id, data.Lot.Id, original.Id), async cancellationToken =>
        {
            context.ParkingMovements.Add(new ParkingMovement(data.User.Id, data.Vehicle.Id, data.Lot.Id, data.Zone.Id,
                PersistenceTestData.Now.AddMinutes(2), data.Guard.Id, original.Id));
            await context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("A duplicate movement identity cannot persist.");
        }, default);
        Assert.Equal("PARKING_MOVEMENT_ID_ALREADY_USED", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await using var verify = fixture.CreateContext();
        Assert.Equal(ParkingMovementStatus.CLOSED, (await verify.ParkingMovements.SingleAsync()).Status);
    }
}
