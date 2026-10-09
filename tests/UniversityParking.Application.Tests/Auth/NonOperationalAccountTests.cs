using Microsoft.Extensions.Logging.Abstractions;
using UniversityParking.Application.Files;
using UniversityParking.Application.Parking;
using UniversityParking.Application.Tests.Parking;
using UniversityParking.Application.Tests.Vehicles;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Tests.Auth;

public sealed class NonOperationalAccountTests
{
    private static User Account(UserStatus status,DateTimeOffset now)
    {
        var user=User.CreateStudentRegistration(new("001"),"Nombre",UniversityIds.Etitc,"Carrera",new("CARD"),status==UserStatus.INACTIVE,now);
        if(status==UserStatus.INACTIVE)user.Deactivate(now);if(status==UserStatus.REJECTED)user.RejectRegistration(now);return user;
    }
    [Theory][InlineData(UserStatus.PENDING)][InlineData(UserStatus.REJECTED)][InlineData(UserStatus.INACTIVE)]
    public void OwnerEligibilityRequiresExplicitActiveState(UserStatus status)
    {
        var user=Account(status,DateTimeOffset.UtcNow);
        foreach(var type in Enum.GetValues<VehicleType>())Assert.Equal("USER_INACTIVE",VehicleOperationContext.Eligibility(user,type)!.Code);
    }
    [Theory][InlineData(UserStatus.PENDING)][InlineData(UserStatus.REJECTED)][InlineData(UserStatus.INACTIVE)]
    public async Task ParkingBlocksNonOperationalTargetWithoutWrites(UserStatus status)
    {
        var context=new ParkingTestContext();var user=Account(status,context.Clock.UtcNow.AddDays(-1));context.Store.OtherUsers.Add(user);
        var result=await context.CheckIn.Handle(context.Request with{UserId=user.Id},default);
        Assert.Equal("USER_INACTIVE",result.Error!.Code);Assert.Equal(0,context.Store.Saves);Assert.Empty(context.Store.Movements);
    }
    [Theory][InlineData(UserStatus.PENDING)][InlineData(UserStatus.REJECTED)][InlineData(UserStatus.INACTIVE)]
    public async Task ParkingBlocksNonOperationalGuardEvenWithGuardClaims(UserStatus status)
    {
        var context=new ParkingTestContext();await context.Store.AddAsync(Account(status,context.Store.UtcNow),default);
        var result=await context.CheckIn.Handle(context.Request,default);
        Assert.Equal("USER_INACTIVE",result.Error!.Code);Assert.Equal(0,context.Store.Saves);Assert.Empty(context.Store.Movements);
    }
    [Theory][InlineData(UserStatus.PENDING)][InlineData(UserStatus.REJECTED)][InlineData(UserStatus.INACTIVE)]
    public async Task VehicleRegistrationBlocksNonOperationalActorBeforeUploads(UserStatus status)
    {
        var store=new VehicleRegistrationTests.Store();await store.AddAsync(Account(status,store.UtcNow),default);
        var operation=new VehicleOperationContext(store,store,store,store,store,store,store,store);
        var handler=new RegisterVehicleCommandHandler(operation,store,store,store,store,store,store,new FileUploadValidator(),store,NullLogger<UploadedFileBatch>.Instance);
        var result=await handler.Handle(new RegisterVehicleCommand(VehicleType.BICYCLE,null,"FRAME123","Marca","Modelo","Negro",[],[]),default);
        Assert.Equal("USER_INACTIVE",result.Error!.Code);Assert.Empty(store.Vehicles);Assert.Empty(store.Keys);Assert.Equal(0,store.Saves);
    }
    [Theory][InlineData(UserStatus.PENDING)][InlineData(UserStatus.REJECTED)][InlineData(UserStatus.INACTIVE)]
    public async Task VehicleRenewalBlocksNonOperationalActorBeforeUploads(UserStatus status)
    {
        var store=new VehicleRegistrationTests.Store();await store.AddAsync(Account(status,store.UtcNow),default);
        var operation=new VehicleOperationContext(store,store,store,store,store,store,store,store);
        var handler=new RenewVehicleRegistrationCommandHandler(operation,store,store,store,store,new FileUploadValidator(),store,NullLogger<UploadedFileBatch>.Instance);
        var result=await handler.Handle(new RenewVehicleRegistrationCommand(Guid.NewGuid(),[]),default);
        Assert.Equal("USER_INACTIVE",result.Error!.Code);Assert.Empty(store.Keys);Assert.Equal(0,store.Saves);Assert.Equal(0,store.Commits);
    }
}
