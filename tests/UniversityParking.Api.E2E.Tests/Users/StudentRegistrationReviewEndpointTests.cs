using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Auditing;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Users;

[Collection("Authentication API")]
public sealed class StudentRegistrationReviewEndpointTests(AuthApiFixture fixture):IAsyncLifetime
{
    private Guid target;private User admin=null!;
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();admin=await fixture.CreateUserAsync("USER","ADMIN");
        var registration=await fixture.Client.PostAsJsonAsync("/api/v1/auth/register/student",new RegisterStudentRequest(Guid.NewGuid().ToString("N"),"Pendiente",UniversityParking.Domain.Universities.UniversityIds.Cmc,"Carrera",Guid.NewGuid().ToString("N"),AuthApiFixture.Password, Email: Guid.NewGuid().ToString("N")+"@example.com", PhoneNumber:"+573001234567"));
        Assert.Equal(HttpStatusCode.Created,registration.StatusCode);target=(await registration.Content.ReadFromJsonAsync<RegisterStudentResponse>())!.UserId;
        await Login(admin);
    }
    public Task DisposeAsync()=>Task.CompletedTask;
    private async Task Login(User user)
    {
        var response=await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,AuthApiFixture.Password));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization=new("Bearer",(await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }
    private Task<HttpResponseMessage> Review(bool approve,Guid? id=null)=>fixture.Client.PatchAsync($"/api/v1/users/{id??target}/registration/{(approve?"approve":"reject")}",null);
    private static async Task Problem(HttpResponseMessage response,HttpStatusCode status,string code)
    {Assert.Equal(status,response.StatusCode);Assert.Equal(code,(await response.Content.ReadFromJsonAsync<ApiProblemDetails>())!.Code);}

    [Theory][InlineData(true)][InlineData(false)]
    public async Task AdminReviewsPendingPreservingIdentityCredentialAndRolesWithReadableAudit(bool approve)
    {
        await using var before=fixture.CreateContext();var previous=await before.Users.AsNoTracking().SingleAsync(x=>x.Id==target);
        var hash=(await before.UserCredentials.SingleAsync(x=>x.UserId==target)).PasswordHash;
        Assert.Equal(HttpStatusCode.NoContent,(await Review(approve)).StatusCode);
        await using var after=fixture.CreateContext();var user=await after.Users.SingleAsync(x=>x.Id==target);
        Assert.Equal(approve?UserStatus.ACTIVE:UserStatus.REJECTED,user.Status);Assert.Equal(previous.CreatedAt,user.CreatedAt);
        Assert.Equal(previous.UniversityId,user.UniversityId);Assert.Equal(previous.CardCode,user.CardCode);Assert.Equal(previous.IdentificationNumber,user.IdentificationNumber);Assert.True(user.UpdatedAt>=previous.UpdatedAt);
        Assert.Equal(hash,(await after.UserCredentials.SingleAsync(x=>x.UserId==target)).PasswordHash);Assert.Single(await after.UserRoles.Where(x=>x.UserId==target).ToArrayAsync());
        var action=approve?"STUDENT_REGISTRATION_APPROVED":"STUDENT_REGISTRATION_REJECTED";
        var audit=await after.AuditLogs.SingleAsync(x=>x.EntityId==target&&x.Action==action);Assert.Equal(admin.Id,audit.ActorUserId);
        using var oldValues=JsonDocument.Parse(audit.OldValues!);using var newValues=JsonDocument.Parse(audit.NewValues!);
        Assert.Equal("PENDING",oldValues.RootElement.GetProperty("Status").GetString());Assert.Equal(user.Status.ToString(),newValues.RootElement.GetProperty("Status").GetString());
        Assert.Equal("Colegio Mayor de Cundinamarca",newValues.RootElement.GetProperty("UniversityName").GetString());Assert.DoesNotContain(hash,audit.NewValues!);Assert.DoesNotContain("Password",audit.NewValues!);
        var login=await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,AuthApiFixture.Password));
        if(approve)Assert.Equal(HttpStatusCode.OK,login.StatusCode);else await Problem(login,HttpStatusCode.Unauthorized,"ACCOUNT_REJECTED");
    }

    [Theory][InlineData("USER",true)][InlineData("USER",false)][InlineData("GUARD",true)][InlineData("GUARD",false)]
    public async Task NonAdminCannotReview(string role,bool approve)
    {await Login(await fixture.CreateUserAsync(role));await Problem(await Review(approve),HttpStatusCode.Forbidden,"FORBIDDEN");await using var db=fixture.CreateContext();Assert.Equal(UserStatus.PENDING,(await db.Users.SingleAsync(x=>x.Id==target)).Status);}

    [Theory][InlineData(true)][InlineData(false)]
    public async Task AnonymousCannotReview(bool approve)
    {fixture.Client.DefaultRequestHeaders.Authorization=null;await Problem(await Review(approve),HttpStatusCode.Unauthorized,"AUTH_INVALID_CREDENTIALS");}

    [Fact] public async Task FailedAuditRollsBackApprovalAndTimestamp()
    {
        await using var before=fixture.CreateContext();var original=await before.Users.AsNoTracking().SingleAsync(x=>x.Id==target);
        await using var factory=fixture.CreateFactory(services=>{services.RemoveAll<IAuditLogRepository>();services.AddScoped<IAuditLogRepository,InvalidAudit>();});
        using var client=factory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        var login=await client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(admin.IdentificationNumber.Value,AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",(await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
        await Problem(await client.PatchAsync($"/api/v1/users/{target}/registration/approve",null),HttpStatusCode.InternalServerError,"INTERNAL_SERVER_ERROR");
        await using var after=fixture.CreateContext();var user=await after.Users.SingleAsync(x=>x.Id==target);
        Assert.Equal(UserStatus.PENDING,user.Status);Assert.Equal(original.UpdatedAt,user.UpdatedAt);Assert.Equal(1,await after.AuditLogs.CountAsync(x=>x.EntityId==target));
    }

    [Theory][InlineData(UserStatus.ACTIVE,true)][InlineData(UserStatus.ACTIVE,false)][InlineData(UserStatus.INACTIVE,true)][InlineData(UserStatus.INACTIVE,false)][InlineData(UserStatus.REJECTED,true)][InlineData(UserStatus.REJECTED,false)]
    public async Task OnlyPendingStateCanBeReviewed(UserStatus state,bool approve)
    {
        await using(var db=fixture.CreateContext())
        {var user=await db.Users.SingleAsync(x=>x.Id==target);if(state==UserStatus.REJECTED)user.RejectRegistration(DateTimeOffset.UtcNow);else{user.ApproveRegistration(DateTimeOffset.UtcNow);if(state==UserStatus.INACTIVE)user.Deactivate(DateTimeOffset.UtcNow);}await db.SaveChangesAsync();}
        await Problem(await Review(approve),HttpStatusCode.Conflict,"INVALID_USER_STATUS_TRANSITION");
        await using var after=fixture.CreateContext();Assert.Equal(state,(await after.Users.SingleAsync(x=>x.Id==target)).Status);Assert.Equal(1,await after.AuditLogs.CountAsync(x=>x.EntityId==target));
    }

    [Theory][InlineData("activate",false)][InlineData("deactivate",false)][InlineData("activate",true)][InlineData("deactivate",true)]
    public async Task OperationalEndpointsCannotApproveOrReopenRegistration(string operation,bool rejected)
    {
        if(rejected){await using var db=fixture.CreateContext();(await db.Users.SingleAsync(x=>x.Id==target)).RejectRegistration(DateTimeOffset.UtcNow);await db.SaveChangesAsync();}
        await Problem(await fixture.Client.PatchAsync($"/api/v1/users/{target}/{operation}",null),HttpStatusCode.Conflict,"INVALID_USER_STATUS_TRANSITION");
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task ConcurrentReviewsProduceOneTransitionAndOneAudit(bool bothApprove)
    {
        var responses=await Task.WhenAll(Review(true),Review(bothApprove));Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.NoContent);
        await Problem(Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict),HttpStatusCode.Conflict,"INVALID_USER_STATUS_TRANSITION");
        await using var db=fixture.CreateContext();Assert.Equal(1,await db.AuditLogs.CountAsync(x=>x.EntityId==target&&x.Action!="STUDENT_REGISTERED"));
        var state=(await db.Users.SingleAsync(x=>x.Id==target)).Status;Assert.True(state is UserStatus.ACTIVE or UserStatus.REJECTED);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task MissingUserReturns404(bool approve)=>await Problem(await Review(approve,Guid.NewGuid()),HttpStatusCode.NotFound,"USER_NOT_FOUND");

    [Fact] public async Task RemovedAdminRoleBlocksOldJwt()
    {
        await using(var db=fixture.CreateContext()){var role=await db.Roles.SingleAsync(x=>x.Code=="ADMIN");await db.UserRoles.Where(x=>x.UserId==admin.Id&&x.RoleId==role.Id).ExecuteDeleteAsync();}
        await Problem(await Review(true),HttpStatusCode.Forbidden,"FORBIDDEN");
    }
    [Theory][InlineData("PENDING")][InlineData("REJECTED")]
    public async Task AdminFilterSupportsNewStatuses(string status)
    {
        if(status=="REJECTED")Assert.Equal(HttpStatusCode.NoContent,(await Review(false)).StatusCode);
        var response=await fixture.Client.GetAsync("/api/v1/users?status="+status);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var page=(await response.Content.ReadFromJsonAsync<PagedResponse<UserListItemResponse>>())!;Assert.Equal(target,Assert.Single(page.Items).Id);Assert.Equal(status,page.Items[0].Status);
    }
    private sealed class InvalidAudit(AppDbContext db):IAuditLogRepository
    {public async Task AddAsync(AuditLog audit,CancellationToken token)=>await db.AuditLogs.AddAsync(new AuditLog(Guid.NewGuid(),audit.Action,audit.EntityType,audit.EntityId,audit.CreatedAt),token);}
}
