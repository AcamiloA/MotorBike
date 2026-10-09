using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Api.RateLimiting;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class RegisterStudentEndpointTests(AuthApiFixture fixture):IAsyncLifetime
{
    private const string Route="/api/v1/auth/register/student";
    public async Task InitializeAsync()
    {await fixture.ResetAsync();await using var db=fixture.CreateContext();db.Roles.Add(new Role("USER"));await db.SaveChangesAsync();}
    public Task DisposeAsync()=>Task.CompletedTask;
    private static RegisterStudentRequest Request()=>new(Guid.NewGuid().ToString("N"),"Estudiante",UniversityIds.Cmc,"Carrera",Guid.NewGuid().ToString("N"),AuthApiFixture.Password);
    private static async Task Problem(HttpResponseMessage response,HttpStatusCode status,string code)
    {
        Assert.Equal(status,response.StatusCode);Assert.Equal("application/problem+json",response.Content.Headers.ContentType!.MediaType);
        var problem=(await response.Content.ReadFromJsonAsync<ApiProblemDetails>())!;Assert.Equal(code,problem.Code);Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Theory][InlineData(false,"PENDING")][InlineData(true,"ACTIVE")]
    public async Task AnonymousRegistrationReturnsMinimal201AndForcedStudentUser(bool auto,string state)
    {
        await using var factory=fixture.CreateFactory(settings:new Dictionary<string,string?>{["StudentRegistration:AutoApprove"]=auto.ToString()});
        using var client=factory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        var response=await client.PostAsJsonAsync(Route,Request());Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[]{"status","userId"},json.RootElement.EnumerateObject().Select(x=>x.Name).Order());Assert.Equal(state,json.RootElement.GetProperty("status").GetString());
        var id=json.RootElement.GetProperty("userId").GetGuid();await using var db=fixture.CreateContext();var user=await db.Users.SingleAsync();
        Assert.Equal(id,user.Id);Assert.Equal(MemberType.STUDENT,user.MemberType);Assert.Equal(state,user.Status.ToString());
        Assert.Equal(UniversityIds.Cmc,user.UniversityId);var role=await db.UserRoles.SingleAsync();Assert.Equal("USER",(await db.Roles.SingleAsync(x=>x.Id==role.RoleId)).Code);
        Assert.Null((await db.AuditLogs.SingleAsync()).ActorUserId);
    }

    [Theory][InlineData("roles","[\"ADMIN\",\"GUARD\"]")][InlineData("memberType","\"STAFF\"")]
    [InlineData("status","\"ACTIVE\"")][InlineData("autoApprove","true")][InlineData("confirmPassword","\"Password1\"")]
    public async Task PrivilegeAndConfirmationFieldsAreRejectedWithoutWrites(string field,string value)
    {
        var json=JsonSerializer.Serialize(Request(),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var content=new StringContent(json[..^1]+",\""+field+"\":"+value+"}",Encoding.UTF8,"application/json");
        await Problem(await fixture.Client.PostAsync(Route,content),HttpStatusCode.BadRequest,"VALIDATION_ERROR");
        await using var db=fixture.CreateContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.UserCredentials.ToListAsync());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task UnknownOrInactiveUniversityUsesExistingErrors(bool inactive)
    {
        await using var db=fixture.CreateContext();var university=await db.Universities.SingleAsync(x=>x.Id==UniversityIds.Cmc);var updated=university.UpdatedAt;
        if(inactive){university.Deactivate(updated.AddDays(1));await db.SaveChangesAsync();}
        try
        {
            await Problem(await fixture.Client.PostAsJsonAsync(Route,Request() with{UniversityId=inactive?UniversityIds.Cmc:Guid.NewGuid()}),
                HttpStatusCode.BadRequest,inactive?"UNIVERSITY_INACTIVE":"UNIVERSITY_NOT_FOUND");Assert.Empty(await db.Users.ToListAsync());
        }
        finally
        {if(inactive)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE universities SET is_active = true, updated_at = {updated} WHERE id = {UniversityIds.Cmc}");}
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task DuplicateIdentificationOrCardReturns409(bool identification)
    {
        var first=Request();Assert.Equal(HttpStatusCode.Created,(await fixture.Client.PostAsJsonAsync(Route,first)).StatusCode);
        var second=identification?Request() with{IdentificationNumber=first.IdentificationNumber}:Request() with{CardCode=first.CardCode};
        await Problem(await fixture.Client.PostAsJsonAsync(Route,second),HttpStatusCode.Conflict,identification?"USER_ALREADY_EXISTS":"USER_CARD_CODE_ALREADY_EXISTS");
        await using var db=fixture.CreateContext();Assert.Equal(1,await db.Users.CountAsync());Assert.Equal(1,await db.UserCredentials.CountAsync());
    }

    [Fact] public async Task SixthRequestIs429AndCannotBypassLimitWithForwardedHeader()
    {
        for(var i=0;i<RateLimitPolicies.StudentRegistrationPermitLimit;i++)
            await Problem(await fixture.Client.PostAsJsonAsync(Route,Request() with{Password="invalid"}),HttpStatusCode.BadRequest,"VALIDATION_ERROR");
        fixture.Client.DefaultRequestHeaders.Add("X-Forwarded-For","203.0.113.99");
        await Problem(await fixture.Client.PostAsJsonAsync(Route,Request()),HttpStatusCode.TooManyRequests,"RATE_LIMIT_EXCEEDED");
        Assert.Equal(HttpStatusCode.OK,(await fixture.Client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await fixture.Client.GetAsync("/api/v1/universities")).StatusCode);
        await Problem(await fixture.Client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest("missing",AuthApiFixture.Password)),HttpStatusCode.Unauthorized,"AUTH_INVALID_CREDENTIALS");
        await Problem(await fixture.Client.PostAsJsonAsync("/api/v1/users",new{}),HttpStatusCode.Unauthorized,"AUTH_INVALID_CREDENTIALS");
        await using var db=fixture.CreateContext();Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact] public void RouteHasAnonymousDedicatedPolicy()
    {
        var endpoint=fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Single(x=>x.RoutePattern.RawText?.Trim('/')==Route.Trim('/'));
        Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Equal(RateLimitPolicies.StudentRegistration,endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Theory][InlineData("university")][InlineData("password")][InlineData("career")][InlineData("name")]
    public async Task RequiredReferenceAndStudentFieldsReturnValidationError(string invalid)
    {
        var request=Request();request=invalid switch
        {"university"=>request with{UniversityId=Guid.Empty},"password"=>request with{Password="weak"},"career"=>request with{Career=" "},_=>request with{FullName=" "}};
        await Problem(await fixture.Client.PostAsJsonAsync(Route,request),HttpStatusCode.BadRequest,"VALIDATION_ERROR");
        await using var db=fixture.CreateContext();Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact] public async Task SwaggerDocumentsMinimalRegistrationAnd201()
    {
        using var json=JsonDocument.Parse(await fixture.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation=json.RootElement.GetProperty("paths").GetProperty(Route).GetProperty("post");
        Assert.True(operation.GetProperty("responses").TryGetProperty("201",out _));
        var schemas=json.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(new[]{"status","userId"},schemas.GetProperty("RegisterStudentResponse").GetProperty("properties").EnumerateObject().Select(x=>x.Name).Order());
        Assert.Equal(new[]{"cardCode","career","fullName","identificationNumber","password","universityId"},schemas.GetProperty("RegisterStudentRequest").GetProperty("properties").EnumerateObject().Select(x=>x.Name).Order());
    }
}
