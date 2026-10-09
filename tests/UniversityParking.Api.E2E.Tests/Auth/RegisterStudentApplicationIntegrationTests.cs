using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Users;
using UniversityParking.Api.E2E.Tests.Users;
using UniversityParking.Application.Auth.Registration;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class RegisterStudentApplicationIntegrationTests(AuthApiFixture fixture):IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();await using var db=fixture.CreateContext();
        db.Roles.AddRange(new Role("USER"),new Role("ADMIN"),new Role("GUARD"));await db.SaveChangesAsync();
    }
    public Task DisposeAsync()=>Task.CompletedTask;
    private static RegisterStudentCommand Request()=>new(Guid.NewGuid().ToString("N"),"Estudiante",UniversityIds.Cmc,"Carrera",Guid.NewGuid().ToString("N"),AuthApiFixture.Password);
    private static async Task<Result<RegisterStudentResult>> Send(IServiceProvider provider,RegisterStudentCommand request)
    {await using var scope=provider.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);}

    [Theory][InlineData(true)][InlineData(false)]
    public async Task RegistrationConfigurationDoesNotChangeAdministrativeCreation(bool auto)
    {
        var actor=await fixture.CreateUserAsync("USER","ADMIN");
        await using var factory=fixture.CreateFactory(settings:new Dictionary<string,string?>{["StudentRegistration:AutoApprove"]=auto.ToString()});
        using var client=factory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        var login=await client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(actor.IdentificationNumber.Value,AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",(await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
        var response=await client.PostJsonAsync("/api/v1/users",new CreateUserRequest(Guid.NewGuid().ToString("N"),"Personal",UniversityIds.Cmc,null,UserMemberType.STAFF,Guid.NewGuid().ToString("N"),AuthApiFixture.Password));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);var id=(await response.Content.ReadFromJsonAsync<UserCreatedResponse>())!.Id;
        await using var db=fixture.CreateContext();var user=await db.Users.SingleAsync(x=>x.Id==id);
        Assert.Equal(UserStatus.ACTIVE,user.Status);Assert.Equal(MemberType.STAFF,user.MemberType);
    }

    [Theory][InlineData(true,UserStatus.ACTIVE)][InlineData(false,UserStatus.PENDING)]
    public async Task MediatorPersistsCompleteAccountAndAnonymousAudit(bool auto,UserStatus expected)
    {
        await using var factory=fixture.CreateFactory(settings:new Dictionary<string,string?>{["StudentRegistration:AutoApprove"]=auto.ToString()});
        var request=Request();var result=await Send(factory.Services,request);Assert.True(result.IsSuccess);Assert.Equal(expected,result.Value.Status);
        await using var db=fixture.CreateContext();var user=await db.Users.SingleAsync();Assert.Equal(result.Value.UserId,user.Id);
        Assert.Equal(MemberType.STUDENT,user.MemberType);Assert.Equal(expected,user.Status);Assert.Equal(UniversityIds.Cmc,user.UniversityId);
        var credential=await db.UserCredentials.SingleAsync();Assert.Equal(user.Id,credential.UserId);
        Assert.True(factory.Services.GetRequiredService<IPasswordHasher>().Verify(request.Password,credential.PasswordHash));
        var assignment=await db.UserRoles.SingleAsync();Assert.Equal("USER",(await db.Roles.SingleAsync(x=>x.Id==assignment.RoleId)).Code);
        var audit=await db.AuditLogs.SingleAsync();Assert.Null(audit.ActorUserId);Assert.Equal("STUDENT_REGISTERED",audit.Action);
        Assert.DoesNotContain(request.Password,audit.NewValues!);Assert.DoesNotContain(credential.PasswordHash,audit.NewValues!);
        using var json=JsonDocument.Parse(audit.NewValues!);Assert.Equal("Colegio Mayor de Cundinamarca",json.RootElement.GetProperty("Profile").GetProperty("UniversityName").GetString());
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task DuplicateRequestDoesNotCreatePartialAccount(bool identification)
    {
        var first=Request();Assert.True((await Send(fixture.Factory.Services,first)).IsSuccess);
        var duplicate=identification?Request() with{IdentificationNumber=first.IdentificationNumber}:Request() with{CardCode=first.CardCode};
        var result=await Send(fixture.Factory.Services,duplicate);Assert.Equal(identification?"USER_ALREADY_EXISTS":"USER_CARD_CODE_ALREADY_EXISTS",result.Error!.Code);
        await AssertOneAccount();
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task ConcurrentDuplicateRequestsHaveOnlyOneWinner(bool identification)
    {
        var first=Request();var second=identification?Request() with{IdentificationNumber=first.IdentificationNumber}:Request() with{CardCode=first.CardCode};
        var results=await Task.WhenAll(Send(fixture.Factory.Services,first),Send(fixture.Factory.Services,second));
        Assert.Single(results,x=>x.IsSuccess);Assert.Equal(identification?"USER_ALREADY_EXISTS":"USER_CARD_CODE_ALREADY_EXISTS",Assert.Single(results,x=>x.IsFailure).Error!.Code);
        await AssertOneAccount();
    }

    [Fact] public async Task InvalidPasswordIsRejectedBeforePersistenceByValidationPipeline()
    {
        var result=await Send(fixture.Factory.Services,Request() with{Password="weak"});Assert.Equal("VALIDATION_ERROR",result.Error!.Code);
        await AssertNoAccount();
    }

    [Fact] public async Task AuditFailureRollsBackAllAccountComponents()
    {
        await using var factory=fixture.CreateFactory(services=>{services.RemoveAll<IAuditLogRepository>();services.AddScoped<IAuditLogRepository,InvalidAudit>();});
        await Assert.ThrowsAsync<DbUpdateException>(()=>Send(factory.Services,Request()));await AssertNoAccount();
    }
    private async Task AssertOneAccount()
    {
        await using var db=fixture.CreateContext();Assert.Equal(1,await db.Users.CountAsync());Assert.Equal(1,await db.UserCredentials.CountAsync());
        Assert.Equal(1,await db.UserRoles.CountAsync());Assert.Equal(1,await db.AuditLogs.CountAsync());
    }
    private async Task AssertNoAccount()
    {
        await using var db=fixture.CreateContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.UserCredentials.ToListAsync());
        Assert.Empty(await db.UserRoles.ToListAsync());Assert.Empty(await db.AuditLogs.ToListAsync());
    }
    private sealed class InvalidAudit(AppDbContext db):IAuditLogRepository
    {public async Task AddAsync(AuditLog audit,CancellationToken token)=>await db.AuditLogs.AddAsync(new AuditLog(Guid.NewGuid(),audit.Action,audit.EntityType,audit.EntityId,audit.CreatedAt),token);}
}
