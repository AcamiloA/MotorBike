using UniversityParking.Application.Auth.Login;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Tests.Auth;

public sealed class LoginStateTests
{
    [Theory]
    [InlineData(UserStatus.ACTIVE,true,null)][InlineData(UserStatus.ACTIVE,false,"AUTH_INVALID_CREDENTIALS")]
    [InlineData(UserStatus.PENDING,true,"ACCOUNT_PENDING")][InlineData(UserStatus.PENDING,false,"AUTH_INVALID_CREDENTIALS")]
    [InlineData(UserStatus.REJECTED,true,"ACCOUNT_REJECTED")][InlineData(UserStatus.REJECTED,false,"AUTH_INVALID_CREDENTIALS")]
    [InlineData(UserStatus.INACTIVE,true,"AUTH_USER_INACTIVE")][InlineData(UserStatus.INACTIVE,false,"AUTH_INVALID_CREDENTIALS")]
    public async Task LoginOnlyReadsRolesAndIssuesTokenForActiveWithValidPassword(UserStatus state,bool valid,string? code)
    {
        var now=DateTimeOffset.UtcNow;var user=User.CreateStudentRegistration(new("001"),"Nombre",UniversityIds.Etitc,"Carrera",new("CARD"),state is UserStatus.ACTIVE or UserStatus.INACTIVE,now);
        if(state==UserStatus.INACTIVE)user.Deactivate(now);if(state==UserStatus.REJECTED)user.RejectRegistration(now);
        var users=new FakeUsers();users.Values.Add(user.Id,user);var hasher=new FakePasswordHasher();
        var credentials=new FakeCredentials{Value=new(user.Id,hasher.Hash("Password1"),now)};
        var roles=new CountingRoles();var tokens=new FakeTokenService();
        var result=await new LoginCommandHandler(users,credentials,roles,hasher,tokens).Handle(new("001",valid?"Password1":"WrongPassword1"),default);
        Assert.Equal(code,result.Error?.Code);Assert.Equal(code is null,result.IsSuccess);Assert.Equal(code is null?1:0,roles.Reads);
        if(code is null){Assert.NotNull(tokens.IssuedFor);Assert.Equal(user.Id,result.Value.User.Id);}else Assert.Null(tokens.IssuedFor);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task MissingCredentialDoesNotRevealPendingOrRejected(bool rejected)
    {
        var now=DateTimeOffset.UtcNow;var user=User.CreateStudentRegistration(new("001"),"Nombre",UniversityIds.Etitc,"Carrera",new("CARD"),false,now);
        if(rejected)user.RejectRegistration(now);var users=new FakeUsers();users.Values.Add(user.Id,user);
        var roles=new CountingRoles();var tokens=new FakeTokenService();
        var result=await new LoginCommandHandler(users,new FakeCredentials(),roles,new FakePasswordHasher(),tokens).Handle(new("001","Password1"),default);
        Assert.Equal("AUTH_INVALID_CREDENTIALS",result.Error!.Code);Assert.Equal(0,roles.Reads);Assert.Null(tokens.IssuedFor);
    }
    private sealed class CountingRoles:IRoleRepository
    {
        public int Reads;public Task<IReadOnlyCollection<string>> GetCodesByUserIdAsync(Guid id,CancellationToken token){Reads++;return Task.FromResult<IReadOnlyCollection<string>>(["USER"]);}
        public Task<Role?> GetByCodeAsync(string code,CancellationToken token)=>throw new NotSupportedException();
        public Task AssignAsync(UserRole role,CancellationToken token)=>throw new NotSupportedException();
        public Task RemoveAsync(Guid id,Guid role,CancellationToken token)=>throw new NotSupportedException();
    }
}
