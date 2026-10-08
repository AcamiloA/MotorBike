using Microsoft.EntityFrameworkCore;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Infrastructure.Persistence.Repositories;

namespace UniversityParking.Infrastructure.Tests.Persistence;

[Collection("PostgreSQL persistence")]
public sealed class RepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Add_ShouldWaitForUnitOfWork_AndQueriesShouldUseNormalizedIdentifiers()
    {
        await using var context = fixture.CreateContext();
        var users = new UserRepository(context);
        var vehicles = new VehicleRepository(context);
        var user = PersistenceTestData.User(" 001A ", " Ab-12 ");
        var vehicle = PersistenceTestData.Vehicle(VehicleType.CAR, "abc-123");
        var bicycle = PersistenceTestData.Vehicle(VehicleType.BICYCLE, " f- 12 ");
        await users.AddAsync(user, CancellationToken.None);
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await vehicles.AddAsync(bicycle, CancellationToken.None);
        await using (var independent = fixture.CreateContext())
        {
            Assert.Equal(0, await independent.Users.CountAsync());
            Assert.Equal(0, await independent.Vehicles.CountAsync());
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(user.Id, (await users.GetByIdAsync(user.Id, CancellationToken.None))!.Id);
        Assert.Equal(user.Id, (await users.GetByIdentificationNumberAsync(new IdentificationNumber("001A"), CancellationToken.None))!.Id);
        Assert.Equal(user.Id, (await users.GetByCardCodeAsync(new CardCode(" Ab-12 "), CancellationToken.None))!.Id);
        Assert.True(await users.ExistsByIdentificationNumberAsync(new IdentificationNumber("001A"), CancellationToken.None));
        Assert.True(await users.ExistsByCardCodeAsync(new CardCode("Ab-12"), CancellationToken.None));
        Assert.False(await users.ExistsByCardCodeAsync(new CardCode("ab-12"), CancellationToken.None));
        Assert.Equal(vehicle.Id, (await vehicles.GetByPlateAsync(new VehiclePlate(" ABC 123 "), CancellationToken.None))!.Id);
        Assert.Equal(vehicle.Id, (await vehicles.GetByIdAsync(vehicle.Id, CancellationToken.None))!.Id);
        Assert.True(await vehicles.ExistsByPlateAsync(new VehiclePlate("abc123"), CancellationToken.None));
        Assert.Equal(bicycle.Id, (await vehicles.GetByFrameNumberAsync(new FrameNumber("F-12"), CancellationToken.None))!.Id);
        Assert.True(await vehicles.ExistsByFrameNumberAsync(new FrameNumber(" f-12 "), CancellationToken.None));
        Assert.Null(await vehicles.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CurrentOwnershipAndRegistrationQueries_ShouldRespectOwnerPeriodAndStatus()
    {
        await using var context = fixture.CreateContext();
        var owner = PersistenceTestData.User();
        var oldOwner = PersistenceTestData.User();
        var vehicle = PersistenceTestData.Vehicle();
        var period = PersistenceTestData.Period(active: true);
        var old = new VehicleOwnership(vehicle.Id, oldOwner.Id, PersistenceTestData.Now, oldOwner.Id);
        old.Close(PersistenceTestData.Now.AddHours(1), "Transferencia");
        context.AddRange(owner, oldOwner, vehicle, period, old);
        var ownerships = new VehicleOwnershipRepository(context);
        var registrations = new VehicleRegistrationRepository(context);
        var ownership = new VehicleOwnership(vehicle.Id, owner.Id, PersistenceTestData.Now.AddHours(1), oldOwner.Id);
        await ownerships.AddAsync(ownership, CancellationToken.None);
        var registration = new VehicleRegistration(vehicle.Id, owner.Id, period.Id, PersistenceTestData.Now.AddHours(1));
        await registrations.AddAsync(registration, CancellationToken.None);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(owner.Id, (await ownerships.GetCurrentByVehicleIdAsync(vehicle.Id, CancellationToken.None))!.UserId);
        Assert.NotNull(await ownerships.GetCurrentByVehicleAndUserAsync(vehicle.Id, owner.Id, CancellationToken.None));
        Assert.Null(await ownerships.GetCurrentByVehicleAndUserAsync(vehicle.Id, oldOwner.Id, CancellationToken.None));
        Assert.NotNull(Assert.Single(await ownerships.GetByUserIdAsync(oldOwner.Id, CancellationToken.None)).EndAt);
        Assert.True(await registrations.ExistsForVehicleUserAndPeriodAsync(vehicle.Id, owner.Id, period.Id, CancellationToken.None));
        Assert.False(await registrations.ExistsForVehicleUserAndPeriodAsync(vehicle.Id, oldOwner.Id, period.Id, CancellationToken.None));
        var loaded = await registrations.GetActiveForVehicleUserAndPeriodAsync(vehicle.Id, owner.Id, period.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        loaded.Cancel(PersistenceTestData.Now.AddHours(2), oldOwner.Id, "OWNERSHIP_TRANSFERRED");
        await context.SaveChangesAsync();
        Assert.Null(await registrations.GetActiveForVehicleUserAndPeriodAsync(vehicle.Id, owner.Id, period.Id, CancellationToken.None));
        Assert.NotNull(await registrations.GetByVehicleUserAndPeriodAsync(vehicle.Id, owner.Id, period.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ParkingAndPeriodRepositories_ShouldReadCurrentState_AndPersistTrackedChanges()
    {
        await using var context = fixture.CreateContext();
        var data = await PersistenceTestData.ParkingAsync(context);
        var periods = new AcademicPeriodRepository(context);
        var planned = PersistenceTestData.Period("2027-1");
        var active = PersistenceTestData.Period(active: true);
        await periods.AddAsync(planned, CancellationToken.None);
        await periods.AddAsync(active, CancellationToken.None);
        var lots = new ParkingLotRepository(context);
        var inactive = new ParkingLot("Otro", "Centro", new TimeOnly(6, 0), new TimeOnly(22, 0), PersistenceTestData.Now);
        inactive.Deactivate(PersistenceTestData.Now);
        await lots.AddAsync(inactive, CancellationToken.None);
        var movements = new ParkingMovementRepository(context);
        var movement = PersistenceTestData.Movement(data);
        await movements.AddAsync(movement, CancellationToken.None);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.True(await periods.ExistsActiveAsync(CancellationToken.None));
        Assert.Equal(active.Id, (await periods.GetActiveAsync(CancellationToken.None))!.Id);
        Assert.Equal(planned.Id, (await periods.GetByIdAsync(planned.Id, CancellationToken.None))!.Id);
        Assert.Equal(data.Lot.Id, Assert.Single(await lots.GetActiveAsync(CancellationToken.None)).Id);
        Assert.NotNull(await lots.GetByIdAsync(inactive.Id, CancellationToken.None));
        Assert.Equal(data.Zone.Id, (await lots.GetZoneForVehicleTypeAsync(data.Lot.Id, VehicleType.MOTORCYCLE, CancellationToken.None))!.Id);
        Assert.Null(await lots.GetZoneForVehicleTypeAsync(data.Lot.Id, VehicleType.CAR, CancellationToken.None));
        Assert.True(await movements.ExistsOpenByVehicleIdAsync(data.Vehicle.Id, CancellationToken.None));
        Assert.True(await movements.ExistsOpenByUserIdAsync(data.User.Id, CancellationToken.None));
        Assert.Equal(movement.Id, (await movements.GetOpenByUserIdAsync(data.User.Id, CancellationToken.None))!.Id);
        var open = (await movements.GetOpenByVehicleIdAsync(data.Vehicle.Id, CancellationToken.None))!;
        open.Close(PersistenceTestData.Now.AddHours(1), data.Guard.Id);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.False(await movements.ExistsOpenByVehicleIdAsync(data.Vehicle.Id, CancellationToken.None));
        Assert.Null(await movements.GetOpenByUserIdAsync(data.User.Id, CancellationToken.None));
        Assert.Equal(ParkingMovementStatus.CLOSED, (await movements.GetByIdAsync(movement.Id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task RemainingEntities_ShouldRoundTrip_WithPrivateSettersAndImmutableAudit()
    {
        await using var context = fixture.CreateContext();
        var data = await PersistenceTestData.ParkingAsync(context);
        var credential = new UserCredential(data.User.Id, "opaque-test-hash", PersistenceTestData.Now);
        var role = new Role(RoleCodes.User);
        var userRole = new UserRole(data.User.Id, role.Id);
        var photo = new VehiclePhoto(data.Vehicle.Id, VehiclePhotoType.GENERAL, "vehicles/test/photos/photo.jpg", "photo.jpg", "image/jpeg", 10, PersistenceTestData.Now);
        var document = new VehicleDocument(data.Vehicle.Id, VehicleDocumentType.INSURANCE, "vehicles/test/documents/doc.pdf", "doc.pdf", "application/pdf", 10, PersistenceTestData.Now);
        var incident = new Incident(data.Lot.Id, data.Guard.Id, IncidentType.DAMAGE, "Daño de prueba", PersistenceTestData.Now, PersistenceTestData.Now);
        var attachment = new IncidentAttachment(incident.Id, "incidents/test/attachments/doc.pdf", "doc.pdf", "application/pdf", 10, PersistenceTestData.Now);
        var news = new NewsItem("Título", "Contenido", data.User.Id, PersistenceTestData.Now);
        news.Publish(PersistenceTestData.Now);
        var audit = new AuditLog(data.User.Id, "VEHICLE_REGISTERED", "Vehicle", data.Vehicle.Id, PersistenceTestData.Now,
            newValues: "{\"plate\":\"ABC123\"}", traceId: "trace-test");
        context.AddRange(credential, role, userRole, photo, document, attachment);
        await new IncidentRepository(context).AddAsync(incident, CancellationToken.None);
        await new NewsRepository(context).AddAsync(news, CancellationToken.None);
        await new AuditLogRepository(context).AddAsync(audit, CancellationToken.None);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal("opaque-test-hash", (await context.UserCredentials.SingleAsync()).PasswordHash);
        Assert.Equal(RoleCodes.User, (await context.Roles.SingleAsync()).Code);
        Assert.Equal(role.Id, (await context.UserRoles.SingleAsync()).RoleId);
        Assert.Equal(VehiclePhotoType.GENERAL, (await context.VehiclePhotos.SingleAsync()).Type);
        Assert.Null((await context.VehicleDocuments.SingleAsync()).ExpiresOn);
        Assert.Equal(IncidentStatus.OPEN, (await new IncidentRepository(context).GetByIdAsync(incident.Id, CancellationToken.None))!.Status);
        Assert.Equal(incident.Id, (await context.IncidentAttachments.SingleAsync()).IncidentId);
        Assert.Equal(NewsStatus.PUBLISHED, (await new NewsRepository(context).GetByIdAsync(news.Id, CancellationToken.None))!.Status);
        var loadedAudit = await context.AuditLogs.SingleAsync();
        Assert.Equal("trace-test", loadedAudit.TraceId);
        Assert.Contains("ABC123", loadedAudit.NewValues!);
    }

    [Fact]
    public async Task UnitOfWorkRollback_ShouldUndoAllPersistedChanges()
    {
        await using var context = fixture.CreateContext();
        await using (var transaction = await context.BeginTransactionAsync(CancellationToken.None))
        {
            await new UserRepository(context).AddAsync(PersistenceTestData.User(), CancellationToken.None);
            await context.SaveChangesAsync();
            await transaction.RollbackAsync(CancellationToken.None);
        }
        await using var verification = fixture.CreateContext();
        Assert.Equal(0, await verification.Users.CountAsync());
    }
}
