using UniversityParking.Domain.Common;
using UniversityParking.Domain.Users;
namespace UniversityParking.Domain.Tests.Users;
public sealed class InstitutionalAccountTests
{
    [Fact] public void PendingStudentCannotChangeInstitutionalTypeToEscapeReview()
    {
        var user=User.CreateStudentRegistration(new("123"),"Student",UniversityParking.Domain.Universities.UniversityIds.Etitc,"Career",new("legacy"),false,DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(()=>user.SetInstitutionalType(InstitutionalUserType.TEACHER));Assert.Equal(InstitutionalUserType.STUDENT,user.UserType);Assert.Equal(UserStatus.PENDING,user.Status);
    }
    [Theory]
    [InlineData(" Andres.AlArCon@Correo.COM ","andres.alarcon@correo.com")]
    [InlineData("a+b@university.edu.co","a+b@university.edu.co")]
    public void EmailNormalizes(string value,string expected)=>Assert.Equal(expected,ContactInformation.Email(value));
    [Theory][InlineData("invalid")][InlineData("a@")][InlineData("a b@example.com")][InlineData("Name <a@example.com>")]
    public void EmailRejectsInvalid(string value)=>Assert.Throws<DomainException>(()=>ContactInformation.Email(value));
    [Theory][InlineData("+57 300 123 4567","+573001234567")][InlineData("(300) 123-4567","3001234567")]
    public void PhoneNormalizes(string value,string expected)=>Assert.Equal(expected,ContactInformation.Phone(value));
    [Theory][InlineData("123")][InlineData("+abc")][InlineData("++573001234567")][InlineData("1234567890123456")]
    public void PhoneRejectsInvalid(string value)=>Assert.Throws<DomainException>(()=>ContactInformation.Phone(value));
    [Theory][InlineData(InstitutionalUserType.STUDENT,"USER")][InlineData(InstitutionalUserType.TEACHER,"USER")][InlineData(InstitutionalUserType.ADMINISTRATIVE,"ADMIN")][InlineData(InstitutionalUserType.GUARD,"GUARD")]
    public void RolesAreDerived(InstitutionalUserType type,string expected)=>Assert.Equal(expected,InstitutionalUsers.Role(type));
    [Fact] public void ChallengeStoresHashAndExpires()
    {
        var now=DateTimeOffset.UtcNow;var challenge=new PasswordChallenge(Guid.NewGuid(),"secret",PasswordChallengePurpose.RECOVERY,now);
        Assert.DoesNotContain("secret",challenge.TokenHash);Assert.True(challenge.Verify("secret",now));Assert.False(challenge.Verify("secret",now.AddMinutes(20)));
    }
    [Fact] public void ChallengeLocksAfterFiveAttempts()
    {
        var now=DateTimeOffset.UtcNow;var challenge=new PasswordChallenge(Guid.NewGuid(),"valid",PasswordChallengePurpose.RECOVERY,now);
        for(var i=0;i<5;i++)Assert.False(challenge.Verify("wrong",now));Assert.Equal(5,challenge.FailedAttempts);Assert.False(challenge.Verify("valid",now));
    }
    [Fact] public void ChallengeConsumedCannotBeReused()
    {
        var now=DateTimeOffset.UtcNow;var challenge=new PasswordChallenge(Guid.NewGuid(),"valid",PasswordChallengePurpose.TEMPORARY_CHANGE,now);
        challenge.Consume(now);Assert.False(challenge.Verify("valid",now));Assert.Equal(now.AddMinutes(5),challenge.ExpiresAt);
    }
}
