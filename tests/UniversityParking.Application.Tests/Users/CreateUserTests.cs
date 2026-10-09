using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Tests.Auth;
using UniversityParking.Application.Users;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Tests.Users;

public sealed class CreateUserTests
{
    private readonly FakeUsers users = new();
    private readonly FakeCredentials credentials = new();
    private readonly FakeRoles roles = new() { Codes = [RoleCodes.User, RoleCodes.Admin] };
    private readonly FakeCurrentUser actor = new() { Roles = [RoleCodes.User, RoleCodes.Admin] };
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly RecordingAudit audits = new();
    private readonly FakeClock clock = new();
    private readonly FakeUniversities universities = new();
    private readonly CreateUserCommandHandler handler;
    public CreateUserTests()
    {
        var admin = new User(new IdentificationNumber("admin"), "Admin", UniversityParking.Domain.Universities.UniversityIds.Etitc, null,
            MemberType.STAFF, new CardCode("admin-card"), clock.UtcNow.AddDays(-1));
        users.Values.Add(admin.Id, admin);
        actor.UserId = admin.Id;
        handler = new(new UserOperationContext(actor, users, roles, audits, clock, new TestRequestContext()),
            users, roles, unitOfWork, universities, new AccountProvisioner(users, credentials, roles, new FakePasswordHasher()));
    }
    private static CreateUserCommand Request() => new("000123", "Estudiante", new Guid("a1100000-0000-4000-8000-000000000001"), "Ingeniería",
        MemberType.STUDENT, "STUDENT-CARD", "TestPassword1", Email:"student@example.com",PhoneNumber:"+573001234567",IdentificationType:"CC");
    [Fact]
    public async Task CreateUser_ShouldCreateStudent_WithUserRole()
    {
        var result = await handler.Handle(Request(), default);
        Assert.True(result.IsSuccess);
        var user = users.Values[result.Value];
        Assert.Equal(MemberType.STUDENT, user.MemberType);
        Assert.Equal(UserStatus.ACTIVE, user.Status);
        Assert.Equal("000123", user.IdentificationNumber.Value);
        Assert.Equal(roles.Values[RoleCodes.User].Id, Assert.Single(roles.Assignments).RoleId);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal("USER_CREATED", Assert.Single(audits.Values).Action);
    }
    [Fact]
    public void CreateUser_ShouldRequireCareer_ForStudent()
    {
        var result = new CreateUserCommandValidator().Validate(Request() with { Career = " " });
        Assert.Contains(result.Errors, x => x.PropertyName == "Career");
    }
    [Fact]
    public async Task CreateUser_ShouldRejectDuplicateIdentificationNumber()
    {
        var result = await handler.Handle(Request() with { IdentificationNumber = " admin " }, default);
        Assert.Equal("USER_ALREADY_EXISTS", result.Error!.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audits.Values);
        Assert.Null(credentials.Value);
    }
    [Fact]
    public async Task CreateUser_IgnoresLegacyCardCodeAndGeneratesUniqueCompatibilityValue()
    {
        var result = await handler.Handle(Request() with { CardCode = " admin-card " }, default);
        Assert.True(result.IsSuccess);Assert.NotEqual("admin-card",users.Values[result.Value].CardCode.Value);
        Assert.Equal(1, unitOfWork.SaveCount);
    }
    [Fact]
    public async Task CreateUser_ShouldHashPassword_AndExcludeItFromAudit()
    {
        var request = Request();
        Assert.True((await handler.Handle(request, default)).IsSuccess);
        Assert.NotEqual(request.InitialPassword, credentials.Value!.PasswordHash);
        var audit = Assert.Single(audits.Values);
        Assert.DoesNotContain(request.InitialPassword, audit.NewValues!);
        Assert.DoesNotContain("Password", audit.NewValues!);
        Assert.Equal("test-trace", audit.TraceId);
    }
    [Theory]
    [InlineData("USER")]
    [InlineData("GUARD")]
    public async Task CreateUser_ShouldRequireAdmin(string role)
    {
        actor.Roles = [role];
        Assert.Equal("FORBIDDEN", (await handler.Handle(Request(), default)).Error!.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Single(users.Values);
    }
    [Fact]
    public async Task CreateUser_ShouldRejectRemovedAdminRoleDespiteOldClaims()
    {
        roles.Codes = [RoleCodes.User];
        Assert.Equal("FORBIDDEN", (await handler.Handle(Request(), default)).Error!.Code);
    }
    [Fact]
    public async Task CreateUser_ShouldAssignAdditionalRolesWithoutDuplicates()
    {
        Assert.True((await handler.Handle(Request() with { Roles = ["GUARD", "GUARD", "USER"] }, default)).IsSuccess);
        Assert.Equal(roles.Values[RoleCodes.User].Id,Assert.Single(roles.Assignments).RoleId);
    }
    [Fact]
    public async Task CreateUser_ShouldRejectMissingRoleBeforeWriting()
    {
        roles.Values.Remove(RoleCodes.User);
        Assert.Equal("ROLE_NOT_FOUND", (await handler.Handle(Request(), default)).Error!.Code);
        Assert.Single(users.Values);
        Assert.Null(credentials.Value);
    }
    [Theory]
    [InlineData("short")]
    [InlineData("onlylowercase1")]
    [InlineData("ONLYUPPERCASE1")]
    [InlineData("NoNumberPassword")]
    public void CreateUser_ShouldValidatePasswordPolicy(string password) =>
        Assert.Contains(new CreateUserCommandValidator().Validate(Request() with { InitialPassword = password }).Errors,
            x => x.PropertyName == "InitialPassword");
    [Theory]
    [InlineData("ROOT")]
    [InlineData("admin")]
    public void CreateUser_ShouldRejectUnknownRoles(string code) =>
        Assert.False(new CreateUserCommandValidator().Validate(Request() with { Roles = [code] }).IsValid);
    [Fact]
    public async Task ProfileUpdate_ShouldRequireCareerFromPersistedMemberType()
    {
        var user = new User(new IdentificationNumber("student"), "Student", UniversityParking.Domain.Universities.UniversityIds.Etitc, "Ingeniería",
            MemberType.STUDENT, new CardCode("student"), clock.UtcNow.AddDays(-1));
        users.Values.Add(user.Id, user);
        actor.UserId = user.Id;
        var update = new UpdateMyProfileCommandHandler(new UserOperationContext(actor, users, roles, audits, clock, new TestRequestContext()), users, unitOfWork, universities);
        var result = await update.Handle(new("Changed", null), default);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
        Assert.Equal("Student", user.FullName);
        Assert.Equal(0, unitOfWork.SaveCount);
    }
    [Fact]
    public void UserQueries_ShouldValidatePaginationAndFilters()
    {
        var result = new GetUsersQueryValidator().Validate(new GetUsersQuery(Page: 0, PageSize: 101, Role: "ROOT", MemberType: (MemberType)99));
        Assert.Equal(4, result.Errors.Count);
    }
    [Theory]
    [InlineData(false, "UNIVERSITY_NOT_FOUND")]
    [InlineData(true, "UNIVERSITY_INACTIVE")]
    public async Task InvalidUniversityDoesNotCreatePartialAccount(bool inactive, string expected)
    {
        if (inactive) universities.University.Deactivate(universities.University.UpdatedAt.AddDays(1));
        var result = await handler.Handle(Request() with { UniversityId = inactive ? universities.University.Id : Guid.NewGuid() }, default);
        Assert.Equal(expected, result.Error!.Code);
        Assert.Single(users.Values);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audits.Values);
    }

    [Fact]
    public void UniversityIdIsRequiredForCreateAndUpdate()
    {
        Assert.Contains(new CreateUserCommandValidator().Validate(Request() with { UniversityId = Guid.Empty }).Errors,
            x => x.PropertyName == "UniversityId");
        Assert.Contains(new UpdateUserCommandValidator().Validate(new UpdateUserCommand(Guid.NewGuid(), "Nombre", Guid.Empty, "Ingeniería", MemberType.STUDENT, "CARD")).Errors,
            x => x.PropertyName == "UniversityId");
    }

    private sealed class RecordingAudit : IAuditLogRepository
    {
        public List<AuditLog> Values { get; } = [];
        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            Values.Add(auditLog);
            return Task.CompletedTask;
        }
    }
    private sealed class TestRequestContext : IRequestContext
    {
        public string? IpAddress => "127.0.0.1";
        public string? TraceId => "test-trace";
    }
}

