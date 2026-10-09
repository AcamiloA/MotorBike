using System.Text.Json;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Auth.Registration;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Users;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Tests.Auth;

public sealed class RegisterStudentTests
{
    private readonly FakeUsers users=new();private readonly FakeCredentials credentials=new();
    private readonly FakeRoles roles=new();private readonly FakeUniversities universities=new();
    private readonly FakeUnitOfWork work=new();private readonly Audit audits=new();private readonly FakeClock clock=new();
    private RegisterStudentCommandHandler Handler(bool auto=false)=>new(users,universities,roles,
        new AccountProvisioner(users,credentials,roles,new FakePasswordHasher()),audits,work,clock,new TestRequestContext(),
        Options.Create(new StudentRegistrationOptions{AutoApprove=auto}));
    private static RegisterStudentCommand Request()=>new(" 000123 "," Nombre ",UniversityIds.Etitc," Carrera "," CARD ","Password1");

    [Theory][InlineData(true,UserStatus.ACTIVE)][InlineData(false,UserStatus.PENDING)]
    public async Task RegistrationForcesStudentAndOnlyUserRoleWithoutAuthenticatedActor(bool auto,UserStatus expected)
    {
        var result=await Handler(auto).Handle(Request(),default);Assert.True(result.IsSuccess);
        var user=Assert.Single(users.Values).Value;Assert.Equal(user.Id,result.Value.UserId);Assert.Equal(expected,result.Value.Status);
        Assert.Equal(expected,user.Status);Assert.Equal(MemberType.STUDENT,user.MemberType);Assert.Equal("000123",user.IdentificationNumber.Value);
        Assert.Equal("CARD",user.CardCode.Value);Assert.Equal("Nombre",user.FullName);Assert.Equal("Carrera",user.Career);
        Assert.Equal(UniversityIds.Etitc,user.UniversityId);Assert.Equal(clock.UtcNow,user.CreatedAt);
        Assert.Equal(user.Id,credentials.Value!.UserId);Assert.NotEqual(Request().Password,credentials.Value.PasswordHash);
        Assert.Equal(roles.Values[RoleCodes.User].Id,Assert.Single(roles.Assignments).RoleId);Assert.Equal(1,work.SaveCount);
        var audit=Assert.Single(audits.Values);Assert.Null(audit.ActorUserId);Assert.Equal("STUDENT_REGISTERED",audit.Action);
        Assert.Equal("test-trace",audit.TraceId);Assert.DoesNotContain("Password",audit.NewValues!);Assert.DoesNotContain(Request().Password,audit.NewValues!);
        using var json=JsonDocument.Parse(audit.NewValues!);Assert.Equal("ETITC",json.RootElement.GetProperty("Profile").GetProperty("UniversityName").GetString());
        Assert.Equal(new[]{"USER"},json.RootElement.GetProperty("Roles").EnumerateArray().Select(x=>x.GetString()));
    }

    [Theory][InlineData(false,"UNIVERSITY_NOT_FOUND")][InlineData(true,"UNIVERSITY_INACTIVE")]
    public async Task InvalidUniversityLeavesNoAccountComponents(bool inactive,string code)
    {
        if(inactive)universities.University.Deactivate(universities.University.UpdatedAt.AddDays(1));
        var result=await Handler().Handle(Request() with {UniversityId=inactive?UniversityIds.Etitc:Guid.NewGuid()},default);
        Assert.Equal(code,result.Error!.Code);Assert.Empty(users.Values);Assert.Null(credentials.Value);Assert.Empty(roles.Assignments);Assert.Empty(audits.Values);Assert.Equal(0,work.SaveCount);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task DuplicateRejectedAccountStillOccupiesIdentificationAndCard(bool identification)
    {
        var user=User.CreateStudentRegistration(new("000123"),"Anterior",UniversityIds.Etitc,"Carrera",new("CARD"),false,clock.UtcNow);
        user.RejectRegistration(clock.UtcNow);users.Values.Add(user.Id,user);
        var request=identification?Request() with{CardCode="OTHER"}:Request() with{IdentificationNumber="OTHER"};
        var result=await Handler().Handle(request,default);
        Assert.Equal(identification?"USER_ALREADY_EXISTS":"USER_CARD_CODE_ALREADY_EXISTS",result.Error!.Code);
        Assert.Single(users.Values);Assert.Null(credentials.Value);Assert.Empty(roles.Assignments);Assert.Empty(audits.Values);
    }

    [Fact] public async Task MissingUserRoleFailsBeforeAnyWrite()
    {
        roles.Values.Remove(RoleCodes.User);var result=await Handler().Handle(Request(),default);
        Assert.Equal("ROLE_NOT_FOUND",result.Error!.Code);Assert.Empty(users.Values);Assert.Null(credentials.Value);Assert.Equal(0,work.SaveCount);
    }

    [Theory][InlineData(null)][InlineData("")][InlineData("short")][InlineData("lowercase1")][InlineData("UPPERCASE1")][InlineData("NoDigitsHere")]
    public void PasswordUsesSameRulesAndMessagesAsAdministrativeCreation(string? password)
    {
        var registration=new RegisterStudentCommandValidator().Validate(Request() with {Password=password!});
        var admin=new CreateUserCommandValidator().Validate(new CreateUserCommand("000123","Nombre",UniversityIds.Etitc,"Carrera",MemberType.STUDENT,"CARD",password!));
        Assert.Equal(admin.Errors.Where(x=>x.PropertyName=="InitialPassword").Select(x=>x.ErrorMessage),
            registration.Errors.Where(x=>x.PropertyName=="Password").Select(x=>x.ErrorMessage));Assert.False(registration.IsValid);
    }

    [Fact] public void RequiredFieldsAndExistingLimitsAreValidated()
    {
        var validator=new RegisterStudentCommandValidator();Assert.True(validator.Validate(Request()).IsValid);
        var invalid=Request() with{IdentificationNumber="",FullName="",UniversityId=Guid.Empty,Career="",CardCode=""};
        Assert.Equal(5,validator.Validate(invalid).Errors.Count);
        Assert.Equal(4,validator.Validate(Request() with{IdentificationNumber=new('a',51),FullName=new('a',201),Career=new('a',201),CardCode=new('a',151)}).Errors.Count);
    }

    [Fact] public async Task CancellationPreventsAccountWrites()
    {
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>Handler().Handle(Request(),canceled.Token));Assert.Empty(users.Values);
    }
    private sealed class Audit:IAuditLogRepository
    {public List<AuditLog> Values {get;}=[];public Task AddAsync(AuditLog value,CancellationToken token){Values.Add(value);return Task.CompletedTask;}}
    private sealed class TestRequestContext:IRequestContext
    {public string? IpAddress=>"127.0.0.1";public string TraceId=>"test-trace";}
}
