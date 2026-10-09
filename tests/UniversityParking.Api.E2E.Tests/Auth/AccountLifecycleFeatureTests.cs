using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Users;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Universities;
using UniversityParking.Api.E2E.Tests.Users;
namespace UniversityParking.Api.E2E.Tests.Auth;
[Collection("Authentication API")]
public sealed class AccountLifecycleFeatureTests(AuthApiFixture fixture):IAsyncLifetime
{
    public async Task InitializeAsync(){await fixture.ResetAsync();await using var db=fixture.CreateContext();db.Roles.AddRange(new Role("USER"),new Role("ADMIN"),new Role("GUARD"));await db.SaveChangesAsync();}public Task DisposeAsync()=>Task.CompletedTask;
    private static CreateUserRequest NewUser(UserInstitutionalType type=UserInstitutionalType.STUDENT)=>new(Guid.NewGuid().ToString("N"),"Test Account",UniversityIds.Etitc,
        type==UserInstitutionalType.STUDENT?"Sistemas":null,UserMemberType.STAFF,null,"TemporaryPassword1",UserType:type,
        Email:Guid.NewGuid().ToString("N")+"@example.com",PhoneNumber:"+57 300 123 4567",IdentificationType:"CC");
    private async Task Authenticate(HttpClient client,User user)
    {var response=await client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,AuthApiFixture.Password));Assert.Equal(HttpStatusCode.OK,response.StatusCode);client.DefaultRequestHeaders.Authorization=new("Bearer",(await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);}
    [Theory][InlineData(UserInstitutionalType.STUDENT,"USER")][InlineData(UserInstitutionalType.TEACHER,"USER")][InlineData(UserInstitutionalType.ADMINISTRATIVE,"ADMIN")][InlineData(UserInstitutionalType.GUARD,"GUARD")]
    public async Task AdministrativeCreationDerivesRoleRequiresPasswordChangeAndNormalizesContact(UserInstitutionalType type,string role)
    {
        var admin=await fixture.CreateUserAsync("ADMIN");await Authenticate(fixture.Client,admin);var request=NewUser(type);
        var response=await fixture.Client.PostJsonAsync("/api/v1/users",request);Assert.Equal(HttpStatusCode.Created,response.StatusCode);var id=(await response.Content.ReadFromJsonAsync<UserCreatedResponse>())!.Id;
        await using var db=fixture.CreateContext();var user=await db.Users.SingleAsync(x=>x.Id==id);
        Assert.Equal(type.ToString(),user.UserType.ToString());Assert.True(user.MustChangePassword);Assert.Equal(UserStatus.ACTIVE,user.Status);Assert.Equal("+573001234567",user.PhoneNumber);
        Assert.Equal(request.Email,user.NormalizedEmail);var assignment=await db.UserRoles.SingleAsync(x=>x.UserId==id);Assert.Equal(role,(await db.Roles.SingleAsync(x=>x.Id==assignment.RoleId)).Code);
        var body=await (await fixture.Client.GetAsync($"/api/v1/users/{id}")).Content.ReadAsStringAsync();Assert.DoesNotContain("passwordHash",body,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("tokenHash",body,StringComparison.OrdinalIgnoreCase);
    }
    [Theory][InlineData("USER")][InlineData("GUARD")]
    public async Task NonAdminCannotCreateAdministrativeUser(string role)
    {var actor=await fixture.CreateUserAsync(role);await Authenticate(fixture.Client,actor);Assert.Equal(HttpStatusCode.Forbidden,(await fixture.Client.PostJsonAsync("/api/v1/users",NewUser(UserInstitutionalType.ADMINISTRATIVE))).StatusCode);}
    [Fact] public async Task TemporaryChallengeCannotAccessNormalApiAndIsConsumedOnce()
    {
        var user=await fixture.CreateUserAsync("USER");await using(var db=fixture.CreateContext()){var account=await db.Users.SingleAsync(x=>x.Id==user.Id);account.RequirePasswordChange();await db.SaveChangesAsync();}
        var login=await (await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,AuthApiFixture.Password))).Content.ReadFromJsonAsync<LoginResponse>();
        Assert.True(login!.RequiresPasswordChange);Assert.Equal("",login.AccessToken);Assert.NotNull(login.ChallengeId);
        fixture.Client.DefaultRequestHeaders.Authorization=new("Bearer",login.PasswordChangeToken);Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Client.GetAsync("/api/v1/users/me")).StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization=null;
        var complete=new CompleteTemporaryPasswordRequest(login.ChallengeId!.Value,login.PasswordChangeToken!,"ChangedPassword2");
        Assert.Equal(HttpStatusCode.NoContent,(await fixture.Client.PostAsJsonAsync("/api/v1/auth/temporary-password/complete",complete)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await fixture.Client.PostAsJsonAsync("/api/v1/auth/temporary-password/complete",complete)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,AuthApiFixture.Password))).StatusCode);
        var normal=await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,"ChangedPassword2"));Assert.Equal(HttpStatusCode.OK,normal.StatusCode);Assert.False((await normal.Content.ReadFromJsonAsync<LoginResponse>())!.RequiresPasswordChange);
    }
    [Fact] public async Task AdministrativeResetRevokesExistingTokenAndRequiresTemporaryChange()
    {
        var target=await fixture.CreateUserAsync("USER");await Authenticate(fixture.Client,target);var old=fixture.Client.DefaultRequestHeaders.Authorization;
        var admin=await fixture.CreateUserAsync("ADMIN");await Authenticate(fixture.Client,admin);
        Assert.Equal(HttpStatusCode.NoContent,(await fixture.Client.PostAsJsonAsync($"/api/v1/users/{target.Id}/reset-password",new ResetUserPasswordRequest("TemporaryPassword2"))).StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization=old;Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Client.GetAsync("/api/v1/users/me")).StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization=null;var login=await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(target.IdentificationNumber.Value,"TemporaryPassword2"));Assert.True((await login.Content.ReadFromJsonAsync<LoginResponse>())!.RequiresPasswordChange);
    }
    private async Task<User> AccountWithEmail()
    {var user=await fixture.CreateUserAsync("USER");await using var db=fixture.CreateContext();(await db.Users.SingleAsync(x=>x.Id==user.Id)).SetContact("user@example.com","+573001234567");await db.SaveChangesAsync();return user;}
    [Fact] public async Task RecoveryDoesNotEnumerateEmailsAndConsumesCodeWithoutReturningIt()
    {
        var user=await AccountWithEmail();var email=new FakeEmails();await using var factory=fixture.CreateFactory(s=>{s.RemoveAll<IEmailSender>();s.AddSingleton<IEmailSender>(email);});using var client=factory.CreateClient(new(){BaseAddress=new("https://localhost")});
        var known=await client.PostAsJsonAsync("/api/v1/auth/password-recovery/request",new RequestPasswordRecoveryRequest(" USER@EXAMPLE.COM "));
        var unknown=await client.PostAsJsonAsync("/api/v1/auth/password-recovery/request",new RequestPasswordRecoveryRequest("missing@example.com"));Assert.Equal(known.StatusCode,unknown.StatusCode);Assert.Equal(await known.Content.ReadAsStringAsync(),await unknown.Content.ReadAsStringAsync());
        Assert.Equal(1,email.Messages);var code=Regex.Match(email.Body!,"[0-9]{8}").Value;Assert.DoesNotContain(code,await known.Content.ReadAsStringAsync());
        await using(var db=fixture.CreateContext()){var challenge=await db.PasswordChallenges.SingleAsync();Assert.NotEqual(code,challenge.TokenHash);Assert.Null(challenge.ConsumedAt);}
        var request=new CompletePasswordRecoveryRequest("user@example.com",code,"RecoveredPassword2");
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/v1/auth/password-recovery/complete",request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/auth/password-recovery/complete",request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,"RecoveredPassword2"))).StatusCode);
    }
    [Fact] public async Task DisabledEmailReturnsSameControlledErrorWithoutCreatingChallenges()
    {
        await AccountWithEmail();var known=await fixture.Client.PostAsJsonAsync("/api/v1/auth/password-recovery/request",new RequestPasswordRecoveryRequest("user@example.com"));var unknown=await fixture.Client.PostAsJsonAsync("/api/v1/auth/password-recovery/request",new RequestPasswordRecoveryRequest("missing@example.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,known.StatusCode);Assert.Equal(known.StatusCode,unknown.StatusCode);await using var db=fixture.CreateContext();Assert.Empty(await db.PasswordChallenges.ToArrayAsync());
    }
    [Fact] public async Task DeliveryFailureRollsBackChallengeReplacementWithControlledError()
    {
        var user=await AccountWithEmail();await using(var db=fixture.CreateContext()){db.PasswordChallenges.Add(new(user.Id,"old-code",PasswordChallengePurpose.RECOVERY,DateTimeOffset.UtcNow));await db.SaveChangesAsync();}
        await using var factory=fixture.CreateFactory(s=>{s.RemoveAll<IEmailSender>();s.AddSingleton<IEmailSender>(new FakeEmails{Fail=true});});using var client=factory.CreateClient(new(){BaseAddress=new("https://localhost")});
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsJsonAsync("/api/v1/auth/password-recovery/request",new RequestPasswordRecoveryRequest("user@example.com"))).StatusCode);
        await using var after=fixture.CreateContext();Assert.Null((await after.PasswordChallenges.SingleAsync()).ConsumedAt);
    }
    [Fact] public async Task TransportFailureReturnsSameErrorForKnownAndUnknownAddresses()
    {
        await AccountWithEmail();await using var factory=fixture.CreateFactory(s=>{s.RemoveAll<IEmailSender>();s.AddSingleton<IEmailSender>(new FakeEmails{FailTransport=true});});using var client=factory.CreateClient(new(){BaseAddress=new("https://localhost")});
        foreach(var address in new[]{"user@example.com","missing@example.com"})
        {var response=await client.PostAsJsonAsync("/api/v1/auth/password-recovery/request",new RequestPasswordRecoveryRequest(address));Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);Assert.Contains("EMAIL_DELIVERY_UNAVAILABLE",await response.Content.ReadAsStringAsync());}
        await using var db=fixture.CreateContext();Assert.Empty(await db.PasswordChallenges.ToArrayAsync());
    }
    private sealed class FakeEmails:IEmailSender
    {public int Messages;public string? Body;public bool Fail;public bool FailTransport;public Task EnsureAvailableAsync(CancellationToken token)=>FailTransport?Task.FromException(new IOException("Synthetic transport failure")):Task.CompletedTask;public Task SendAsync(string to,string subject,string body,CancellationToken token){if(Fail)throw new InvalidOperationException("Synthetic delivery failure");Messages++;Body=body;return Task.CompletedTask;}}
}
