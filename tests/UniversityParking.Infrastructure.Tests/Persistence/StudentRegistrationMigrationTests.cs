using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;

namespace UniversityParking.Infrastructure.Tests.Persistence;

[Collection("PostgreSQL persistence")]
public sealed class StudentRegistrationMigrationTests(PostgresFixture fixture):IAsyncLifetime
{
    private const string Previous="20261008140000_AddUniversityCatalog";
    private const string Current="20261008214600_AddStudentRegistrationStatuses";
    public Task InitializeAsync()=>fixture.ResetAsync();
    public Task DisposeAsync()=>Task.CompletedTask;
    private static User Student()=>User.CreateStudentRegistration(new(Guid.NewGuid().ToString("N")),"Estudiante",UniversityIds.Cmc,"Carrera",new(Guid.NewGuid().ToString("N")),false,PersistenceTestData.Now);

    [Theory][InlineData(UserStatus.ACTIVE)][InlineData(UserStatus.INACTIVE)][InlineData(UserStatus.PENDING)][InlineData(UserStatus.REJECTED)]
    public async Task NewConstraintPersistsAllFourDomainStates(UserStatus state)
    {
        await using var db=fixture.CreateContext();var user=Student();
        if(state is UserStatus.ACTIVE or UserStatus.INACTIVE)user.ApproveRegistration(PersistenceTestData.Now);
        if(state==UserStatus.INACTIVE)user.Deactivate(PersistenceTestData.Now);
        if(state==UserStatus.REJECTED)user.RejectRegistration(PersistenceTestData.Now);
        db.Users.Add(user);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        Assert.Equal(state,(await db.Users.SingleAsync()).Status);
    }

    [Fact] public async Task ConstraintStillRejectsUnknownStates()
    {
        await using var db=fixture.CreateContext();db.Users.Add(Student());await db.SaveChangesAsync();
        var error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE users SET status = 'UNKNOWN'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation,error.SqlState);Assert.Equal("ck_users_status",error.ConstraintName);
    }

    [Fact] public async Task MigrationPreservesExistingActiveInactiveUsersCredentialsAndRoles()
    {
        await using var db=fixture.CreateContext();var migrator=db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        try
        {
            var active=PersistenceTestData.User();var inactive=PersistenceTestData.User();inactive.Deactivate(PersistenceTestData.Now.AddMinutes(1));
            var role=new Role(RoleCodes.User);db.AddRange(active,inactive,role,new UserRole(active.Id,role.Id),new UserCredential(active.Id,"PreservedTestHash",PersistenceTestData.Now));
            await db.SaveChangesAsync();var before=await db.Users.AsNoTracking().OrderBy(x=>x.Id)
                .Select(x=>new {x.Id,x.FullName,x.UniversityId,x.Status,x.CreatedAt,x.UpdatedAt}).ToArrayAsync();
            await migrator.MigrateAsync();
            Assert.Equal(before,await db.Users.AsNoTracking().OrderBy(x=>x.Id)
                .Select(x=>new {x.Id,x.FullName,x.UniversityId,x.Status,x.CreatedAt,x.UpdatedAt}).ToArrayAsync());
            Assert.Equal("PreservedTestHash",(await db.UserCredentials.SingleAsync()).PasswordHash);
            Assert.Equal(role.Id,(await db.UserRoles.SingleAsync()).RoleId);Assert.Equal(3,await db.Universities.CountAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            await migrator.MigrateAsync(Previous);
            Assert.Equal(before,await db.Users.AsNoTracking().OrderBy(x=>x.Id)
                .Select(x=>new {x.Id,x.FullName,x.UniversityId,x.Status,x.CreatedAt,x.UpdatedAt}).ToArrayAsync());
        }
        finally {await migrator.MigrateAsync();}
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task DowngradeFailsWithoutRemappingPendingOrRejected(bool rejected)
    {
        await using var db=fixture.CreateContext();var user=Student();if(rejected)user.RejectRegistration(PersistenceTestData.Now);
        db.Users.Add(user);await db.SaveChangesAsync();var migrator=db.GetService<IMigrator>();
        var error=await Assert.ThrowsAsync<PostgresException>(()=>migrator.MigrateAsync(Previous));
        Assert.Equal("P0001",error.SqlState);Assert.Contains(Current,await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(rejected?UserStatus.REJECTED:UserStatus.PENDING,(await db.Users.AsNoTracking().SingleAsync()).Status);
    }
}
