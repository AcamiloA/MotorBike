using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class LoginStateEndpointTests(AuthApiFixture fixture):IAsyncLifetime
{
    public Task InitializeAsync()=>fixture.ResetAsync();public Task DisposeAsync()=>Task.CompletedTask;
    private async Task<User> Account(UserStatus state)
    {
        var now=DateTimeOffset.UtcNow;var user=User.CreateStudentRegistration(new(Guid.NewGuid().ToString("N")),"Nombre",UniversityIds.Etitc,"Carrera",new(Guid.NewGuid().ToString("N")),state is UserStatus.ACTIVE or UserStatus.INACTIVE,now);
        if(state==UserStatus.INACTIVE)user.Deactivate(now);if(state==UserStatus.REJECTED)user.RejectRegistration(now);
        await using var db=fixture.CreateContext();var role=new Role("USER");db.AddRange(user,role,new UserRole(user.Id,role.Id),new UserCredential(user.Id,fixture.Factory.Services.GetRequiredService<IPasswordHasher>().Hash(AuthApiFixture.Password),now));await db.SaveChangesAsync();return user;
    }
    [Theory]
    [InlineData(UserStatus.ACTIVE,true,null)][InlineData(UserStatus.ACTIVE,false,"AUTH_INVALID_CREDENTIALS")]
    [InlineData(UserStatus.PENDING,true,"ACCOUNT_PENDING")][InlineData(UserStatus.PENDING,false,"AUTH_INVALID_CREDENTIALS")]
    [InlineData(UserStatus.REJECTED,true,"ACCOUNT_REJECTED")][InlineData(UserStatus.REJECTED,false,"AUTH_INVALID_CREDENTIALS")]
    [InlineData(UserStatus.INACTIVE,true,"AUTH_USER_INACTIVE")][InlineData(UserStatus.INACTIVE,false,"AUTH_INVALID_CREDENTIALS")]
    public async Task HttpLoginRevealsStateOnlyAfterValidPassword(UserStatus state,bool valid,string? code)
    {
        var user=await Account(state);var response=await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(user.IdentificationNumber.Value,valid?AuthApiFixture.Password:"WrongPassword1"));
        if(code is null){Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.False(string.IsNullOrWhiteSpace((await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken));}
        else
        {
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);Assert.Equal("application/problem+json",response.Content.Headers.ContentType!.MediaType);
            var problem=(await response.Content.ReadFromJsonAsync<ApiProblemDetails>())!;Assert.Equal(code,problem.Code);
            if(code=="ACCOUNT_PENDING")Assert.Equal("Tu registro está pendiente de aprobación.",problem.Detail);
            if(code=="ACCOUNT_REJECTED")Assert.Equal("Tu solicitud de registro no fue aprobada.",problem.Detail);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());Assert.False(json.RootElement.TryGetProperty("accessToken",out _));
        }
    }
    [Theory][InlineData(UserStatus.PENDING)][InlineData(UserStatus.REJECTED)][InlineData(UserStatus.INACTIVE)]
    public async Task NonActiveAccountWithSimulatedStaleTokenCannotAccessProtectedProfile(UserStatus state)
    {
        var user=await Account(state);await using var scope=fixture.Factory.Services.CreateAsyncScope();var token=scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(new TokenUser(user.Id,user.MemberType,["USER"]));
        fixture.Client.DefaultRequestHeaders.Authorization=new("Bearer",token.Token);
        var response=await fixture.Client.GetAsync("/api/v1/users/me");Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        Assert.Equal("USER_INACTIVE",(await response.Content.ReadFromJsonAsync<ApiProblemDetails>())!.Code);
    }
}
