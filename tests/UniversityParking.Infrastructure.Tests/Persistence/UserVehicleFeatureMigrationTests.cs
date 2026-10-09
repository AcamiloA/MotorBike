using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;
namespace UniversityParking.Infrastructure.Tests.Persistence;
[Collection("PostgreSQL persistence")]
public sealed class UserVehicleFeatureMigrationTests(PostgresFixture fixture):IAsyncLifetime
{
    public Task InitializeAsync()=>fixture.ResetAsync();public Task DisposeAsync()=>Task.CompletedTask;
    [Fact] public async Task BackfillUsesAdminGuardUserPriorityWithoutInventingContactOrTeachers()
    {
        await using var db=fixture.CreateContext();var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync("20261009011853_AddVehicleVerificationImages");
        try
        {
            var first=PersistenceTestData.User();var second=PersistenceTestData.User();var third=PersistenceTestData.User();
            await PersistenceTestData.InsertLegacyUser(db,first);await PersistenceTestData.InsertLegacyUser(db,second);await PersistenceTestData.InsertLegacyUser(db,third);
            var admin=new Role("ADMIN");var guard=new Role("GUARD");var ordinary=new Role("USER");db.AddRange(admin,guard,ordinary,new UserRole(first.Id,admin.Id),new UserRole(first.Id,guard.Id),new UserRole(first.Id,ordinary.Id),new UserRole(second.Id,guard.Id),new UserRole(second.Id,ordinary.Id),new UserRole(third.Id,ordinary.Id));await db.SaveChangesAsync();
            await migrator.MigrateAsync();db.ChangeTracker.Clear();var users=await db.Users.ToDictionaryAsync(x=>x.Id);
            Assert.Equal(InstitutionalUserType.ADMINISTRATIVE,users[first.Id].UserType);Assert.Equal(InstitutionalUserType.GUARD,users[second.Id].UserType);Assert.Equal(InstitutionalUserType.STUDENT,users[third.Id].UserType);
            Assert.All(users.Values,u=>{Assert.Null(u.Email);Assert.Null(u.PhoneNumber);Assert.False(u.MustChangePassword);Assert.Equal(UserStatus.ACTIVE,u.Status);});Assert.Equal(6,await db.UserRoles.CountAsync());Assert.False(db.Database.HasPendingModelChanges());
        }
        finally{await migrator.MigrateAsync();}
    }
    [Fact] public async Task NormalizedEmailUniquenessRejectsCaseVariant()
    {
        await using var db=fixture.CreateContext();var first=PersistenceTestData.User();first.SetContact("USER@EXAMPLE.COM","+573001234567");db.Add(first);await db.SaveChangesAsync();
        var second=PersistenceTestData.User();second.SetContact(" user@example.com ","+573001234568");db.Add(second);await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
    [Fact] public async Task ArchivedScooterSerialCanBeReusedWithoutRemovingHistoricalVehicle()
    {
        await using var db=fixture.CreateContext();var first=new Vehicle(VehicleType.SCOOTER,null,new("SERIAL-1"),"brand","model","color",PersistenceTestData.Now);db.Add(first);await db.SaveChangesAsync();
        first.Archive(PersistenceTestData.Now.AddMinutes(1));await db.SaveChangesAsync();var second=new Vehicle(VehicleType.SCOOTER,null,new("SERIAL-1"),"brand","model","color",PersistenceTestData.Now.AddMinutes(2));db.Add(second);await db.SaveChangesAsync();Assert.Equal(2,await db.Vehicles.CountAsync());
    }
}
