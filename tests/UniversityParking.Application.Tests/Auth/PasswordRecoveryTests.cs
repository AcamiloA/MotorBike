using Microsoft.Extensions.Logging.Abstractions;
using UniversityParking.Application.Auth.Login;
using UniversityParking.Application.Auth.PasswordRecovery;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;
namespace UniversityParking.Application.Tests.Auth;
public sealed class PasswordRecoveryTests
{
    private readonly FakeUsers users=new();private readonly FakeCredentials credentials=new();private readonly FakeClock clock=new();
    private readonly FakeUnitOfWork work=new();private readonly Challenges challenges=new();private readonly Emails emails=new();private readonly FakePasswordHasher hasher=new();
    private readonly User user;
    public PasswordRecoveryTests()
    {
        user=new(new("123"),"Account",UniversityParking.Domain.Universities.UniversityIds.Etitc,"Career",MemberType.STUDENT,new("legacy"),clock.UtcNow);
        user.SetContact("user@example.com","+573001234567");users.Values.Add(user.Id,user);credentials.Value=new(user.Id,hasher.Hash("Password1"),clock.UtcNow);
    }
    private PasswordChallengeService Service=>new(challenges,clock);
    private RequestPasswordRecoveryHandler Request=>new(users,Service,work,emails,NullLogger<RequestPasswordRecoveryHandler>.Instance);
    private CompletePasswordRecoveryHandler Complete=>new(users,challenges,credentials,hasher,Service,work,clock);
    [Fact] public async Task UnknownEmailReturnsSameSuccessWithoutEmailOrChallenge()
    {Assert.True((await Request.Handle(new("missing@example.com"),default)).IsSuccess);Assert.Empty(challenges.Items);Assert.Null(emails.Body);}
    [Fact] public async Task RecoveryHashesCodeInvalidatesPreviousAndConsumesOnce()
    {
        await Request.Handle(new("user@example.com"),default);var first=challenges.Items.Single();
        await Request.Handle(new("user@example.com"),default);Assert.NotNull(first.ConsumedAt);
        var code=System.Text.RegularExpressions.Regex.Match(emails.Body!,"[0-9]{8}").Value;var latest=challenges.Items.Last();Assert.NotEqual(code,latest.TokenHash);
        var stamp=credentials.Value!.SecurityStamp;
        Assert.True((await Complete.Handle(new CompletePasswordRecoveryCommand("user@example.com",code,"DifferentPassword2"),default)).IsSuccess);
        Assert.False(hasher.Verify("Password1",credentials.Value.PasswordHash));Assert.True(hasher.Verify("DifferentPassword2",credentials.Value.PasswordHash));Assert.NotEqual(stamp,credentials.Value.SecurityStamp);
        Assert.True((await Complete.Handle(new CompletePasswordRecoveryCommand("user@example.com",code,"OtherPassword3"),default)).IsFailure);
    }
    [Fact] public async Task ExpiredRecoveryAndFiveWrongCodesCannotReset()
    {
        var created=await Service.CreateAsync(user.Id,PasswordChallengePurpose.RECOVERY,default);
        clock.UtcNow=clock.UtcNow.AddMinutes(20);Assert.True((await Complete.Handle(new CompletePasswordRecoveryCommand("user@example.com",created.Token,"OtherPassword2"),default)).IsFailure);
        clock.UtcNow=clock.UtcNow.AddMinutes(1);created=await Service.CreateAsync(user.Id,PasswordChallengePurpose.RECOVERY,default);
        for(var i=0;i<5;i++)Assert.True((await Complete.Handle(new CompletePasswordRecoveryCommand("user@example.com","wrong","OtherPassword2"),default)).IsFailure);
        Assert.True((await Complete.Handle(new CompletePasswordRecoveryCommand("user@example.com",created.Token,"OtherPassword2"),default)).IsFailure);
        Assert.True(hasher.Verify("Password1",credentials.Value!.PasswordHash));
    }
    [Fact] public async Task TemporaryLoginReturnsOnlyLimitedChallengeThenNewPasswordWorks()
    {
        user.RequirePasswordChange();var tokens=new FakeTokenService();var login=new LoginCommandHandler(users,credentials,new FakeRoles(),hasher,tokens,Service,work);
        var result=await login.Handle(new("123","Password1"),default);Assert.True(result.IsSuccess);Assert.True(result.Value.RequiresPasswordChange);Assert.Equal("",result.Value.AccessToken);Assert.Null(tokens.IssuedFor);
        var challenge=challenges.Items.Single();Assert.Equal(PasswordChallengePurpose.TEMPORARY_CHANGE,challenge.Purpose);
        Assert.True((await Complete.Handle(new CompleteTemporaryPasswordCommand(result.Value.ChallengeId!.Value,result.Value.PasswordChangeToken!,"DifferentPassword2"),default)).IsSuccess);
        Assert.False(user.MustChangePassword);Assert.True((await login.Handle(new("123","Password1"),default)).IsFailure);
        var normal=await login.Handle(new("123","DifferentPassword2"),default);Assert.False(normal.Value.RequiresPasswordChange);Assert.NotNull(tokens.IssuedFor);
        Assert.True((await Complete.Handle(new CompleteTemporaryPasswordCommand(challenge.Id,result.Value.PasswordChangeToken!,"OtherPassword3"),default)).IsFailure);
    }
    private sealed class Challenges:IPasswordChallengeRepository
    {
        public List<PasswordChallenge> Items {get;}=[];
        public Task<PasswordChallenge?> GetAsync(Guid id,CancellationToken token)=>Task.FromResult(Items.FirstOrDefault(x=>x.Id==id));
        public Task<IReadOnlyList<PasswordChallenge>> GetPendingAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<PasswordChallenge>>(Items.Where(x=>x.UserId==id&&x.ConsumedAt==null).ToArray());
        public Task AddAsync(PasswordChallenge item,CancellationToken token){Items.Add(item);return Task.CompletedTask;}
    }
    private sealed class Emails:IEmailSender
    {public string? Body;public Task SendAsync(string to,string subject,string body,CancellationToken token){Body=body;return Task.CompletedTask;}}
}
