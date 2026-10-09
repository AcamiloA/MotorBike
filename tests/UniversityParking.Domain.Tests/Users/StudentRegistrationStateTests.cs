using UniversityParking.Domain.Common;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Domain.Tests.Users;

public sealed class StudentRegistrationStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 21, 46, 0, TimeSpan.Zero);
    private static User Student(bool auto = false) => User.CreateStudentRegistration(new("001"),
        "Nombre", UniversityIds.Cmc, "Carrera", new("CARD"), auto, Now);
    private static User WithState(UserStatus status)
    {
        var user = Student(status is UserStatus.ACTIVE or UserStatus.INACTIVE);
        if (status == UserStatus.INACTIVE) user.Deactivate(Now.AddMinutes(1));
        if (status == UserStatus.REJECTED) user.RejectRegistration(Now.AddMinutes(1));
        return user;
    }

    [Fact] public void EnumRetainsExistingOrdinals()
    { Assert.Equal(0,(int)UserStatus.ACTIVE); Assert.Equal(1,(int)UserStatus.INACTIVE); Assert.Equal(2,(int)UserStatus.PENDING); Assert.Equal(3,(int)UserStatus.REJECTED); }

    [Theory][InlineData(true,UserStatus.ACTIVE)][InlineData(false,UserStatus.PENDING)]
    public void FactoryCreatesOnlyStudentWithControlledInitialState(bool auto,UserStatus expected)
    {
        var user=Student(auto);Assert.Equal(expected,user.Status);Assert.Equal(MemberType.STUDENT,user.MemberType);
        Assert.Equal(UniversityIds.Cmc,user.UniversityId);Assert.Equal(Now,user.CreatedAt);Assert.Equal(Now,user.UpdatedAt);
    }

    [Theory][InlineData(MemberType.STUDENT)][InlineData(MemberType.TEACHER)][InlineData(MemberType.STAFF)]
    public void ExistingConstructorRemainsActive(MemberType member)
    {
        var user=new User(new IdentificationNumber("001"),"Nombre",UniversityIds.Etitc,"Carrera",member,new CardCode("CARD"),Now);
        Assert.Equal(UserStatus.ACTIVE,user.Status);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public void ReviewChangesOnlyStatusAndUpdateTime(bool approve)
    {
        var user=Student();var id=user.Id;var reviewed=Now.AddMinutes(2);
        if(approve)user.ApproveRegistration(reviewed);else user.RejectRegistration(reviewed);
        Assert.Equal(approve?UserStatus.ACTIVE:UserStatus.REJECTED,user.Status);Assert.Equal(reviewed,user.UpdatedAt);
        Assert.Equal(Now,user.CreatedAt);Assert.Equal(id,user.Id);Assert.Equal("001",user.IdentificationNumber.Value);
        Assert.Equal("Nombre",user.FullName);Assert.Equal("Carrera",user.Career);Assert.Equal("CARD",user.CardCode.Value);Assert.Equal(UniversityIds.Cmc,user.UniversityId);
    }

    [Theory]
    [InlineData(UserStatus.ACTIVE,true)][InlineData(UserStatus.ACTIVE,false)]
    [InlineData(UserStatus.INACTIVE,true)][InlineData(UserStatus.INACTIVE,false)]
    [InlineData(UserStatus.REJECTED,true)][InlineData(UserStatus.REJECTED,false)]
    public void ReviewRejectsAllNonPendingStates(UserStatus state,bool approve)
    {
        var user=WithState(state);var updated=user.UpdatedAt;
        var error=Assert.Throws<DomainException>(()=>{if(approve)user.ApproveRegistration(Now.AddMinutes(2));else user.RejectRegistration(Now.AddMinutes(2));});
        Assert.Equal("INVALID_USER_STATUS_TRANSITION",error.Code);Assert.Equal(state,user.Status);Assert.Equal(updated,user.UpdatedAt);
    }

    [Theory][InlineData(UserStatus.PENDING,true)][InlineData(UserStatus.PENDING,false)]
    [InlineData(UserStatus.REJECTED,true)][InlineData(UserStatus.REJECTED,false)]
    public void OperationalTransitionsNeverApproveOrReopenRegistration(UserStatus state,bool activate)
    {
        var user=WithState(state);var updated=user.UpdatedAt;
        Assert.Equal("INVALID_USER_STATUS_TRANSITION",Assert.Throws<DomainException>(()=>{if(activate)user.Activate(Now.AddMinutes(2));else user.Deactivate(Now.AddMinutes(2));}).Code);
        Assert.Equal(state,user.Status);Assert.Equal(updated,user.UpdatedAt);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public void InvalidReviewTimeDoesNotMutateState(bool approve)
    {
        var user=Student();Assert.Throws<DomainException>(()=>{if(approve)user.ApproveRegistration(Now.AddSeconds(-1));else user.RejectRegistration(Now.AddSeconds(-1));});
        Assert.Equal(UserStatus.PENDING,user.Status);Assert.Equal(Now,user.UpdatedAt);
    }

    [Fact] public void ActiveInactiveTransitionsRemainIdempotentAndPreserveIdentity()
    {
        var user=Student(true);var id=user.Id;user.Activate(Now.AddMinutes(1));Assert.Equal(Now,user.UpdatedAt);
        user.Deactivate(Now.AddMinutes(2));user.Deactivate(Now.AddMinutes(3));Assert.Equal(Now.AddMinutes(2),user.UpdatedAt);
        user.Activate(Now.AddMinutes(4));Assert.Equal(UserStatus.ACTIVE,user.Status);Assert.Equal(Now.AddMinutes(4),user.UpdatedAt);Assert.Equal(id,user.Id);
    }

    [Fact] public void PendingNonStudentCannotBeApprovedOrRejected()
    {
        var user=Student();user.Update(user.FullName,user.UniversityId,null,MemberType.STAFF,user.CardCode,Now);
        Assert.Throws<DomainException>(()=>user.ApproveRegistration(Now));Assert.Throws<DomainException>(()=>user.RejectRegistration(Now));Assert.Equal(UserStatus.PENDING,user.Status);
    }
}
