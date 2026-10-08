using UniversityParking.Application.Auth.ChangePassword;
using UniversityParking.Application.Auth.Login;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Tests.Auth;

public sealed class AuthHandlerTests
{
    private const string Password = "InitialPassword1";
    private readonly FakeClock clock = new();
    private readonly FakeCurrentUser actor = new();
    private readonly FakeUsers users = new();
    private readonly FakeCredentials credentials = new();
    private readonly FakeRoles roles = new();
    private readonly FakePasswordHasher hasher = new();
    private readonly FakeTokenService tokens = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly User user;

    public AuthHandlerTests()
    {
        user = new User(new IdentificationNumber("001A"), "Usuario de prueba", UniversityParking.Domain.Universities.UniversityIds.Etitc, null,
            MemberType.STAFF, new CardCode("Card1"), clock.UtcNow.AddDays(-1));
        users.Values.Add(user.Id, user);
        credentials.Value = new UserCredential(user.Id, hasher.Hash(Password), clock.UtcNow.AddDays(-1));
        actor.UserId = user.Id;
    }
    private LoginCommandHandler Login() => new(users, credentials, roles, hasher, tokens);
    private ChangePasswordCommandHandler Change() => new(actor, users, credentials, hasher, clock, unitOfWork);

    [Fact]
    public async Task Login_ShouldNormalizeIdentification_AndReturnMinimalSessionWithRoles()
    {
        roles.Codes = [RoleCodes.User, RoleCodes.Admin, RoleCodes.Guard];
        var result = await Login().Handle(new LoginCommand(" 001A ", Password), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(roles.Codes, result.Value.User.Roles);
        Assert.Equal("test-only-token", result.Value.AccessToken);
        Assert.Equal(user.Id, tokens.IssuedFor!.Id);
        Assert.Equal(MemberType.STAFF, result.Value.User.MemberType);
    }

    [Theory]
    [InlineData("missing", "WrongPassword1")]
    [InlineData("001A", "WrongPassword1")]
    public async Task Login_ShouldReturnSameErrorForMissingUserAndWrongPassword(string identification, string password)
    {
        var result = await Login().Handle(new LoginCommand(identification, password), CancellationToken.None);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", result.Error!.Code);
        Assert.Null(tokens.IssuedFor);
    }

    [Fact]
    public async Task Login_ShouldRejectInactiveUser_OnlyAfterPasswordVerification()
    {
        user.Deactivate(clock.UtcNow);
        var invalid = await Login().Handle(new LoginCommand("001A", "WrongPassword1"), CancellationToken.None);
        var inactive = await Login().Handle(new LoginCommand("001A", Password), CancellationToken.None);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", invalid.Error!.Code);
        Assert.Equal("AUTH_USER_INACTIVE", inactive.Error!.Code);
        Assert.Null(tokens.IssuedFor);
    }

    [Fact]
    public async Task Login_ShouldRejectMissingCredential()
    {
        credentials.Value = null;
        var result = await Login().Handle(new LoginCommand("001A", Password), CancellationToken.None);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", result.Error!.Code);
        Assert.Null(tokens.IssuedFor);
    }

    [Fact]
    public async Task ChangePassword_ShouldSaveNewHashAndClockTimestamp_ForAuthenticatedUser()
    {
        using var source = new CancellationTokenSource();
        var result = await Change().Handle(new ChangePasswordCommand(Password, "DifferentPassword2"), source.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal(hasher.Hash("DifferentPassword2"), credentials.Value!.PasswordHash);
        Assert.Equal(clock.UtcNow, credentials.Value.PasswordChangedAt);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(source.Token, unitOfWork.LastToken);
    }

    [Theory]
    [InlineData("unauthenticated", "AUTH_INVALID_CREDENTIALS")]
    [InlineData("inactive", "USER_INACTIVE")]
    [InlineData("missing", "USER_NOT_FOUND")]
    [InlineData("wrong-password", "AUTH_INVALID_CREDENTIALS")]
    [InlineData("same-password", "VALIDATION_ERROR")]
    public async Task ChangePassword_ShouldRejectInvalidOperation_WithoutSaving(string situation, string code)
    {
        if (situation == "unauthenticated") actor.IsAuthenticated = false;
        if (situation == "inactive") user.Deactivate(clock.UtcNow);
        if (situation == "missing") users.Values.Clear();
        var originalHash = credentials.Value!.PasswordHash;
        var request = new ChangePasswordCommand(situation == "wrong-password" ? "WrongPassword1" : Password,
            situation == "same-password" ? Password : "DifferentPassword2");
        var result = await Change().Handle(request, CancellationToken.None);
        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(originalHash, credentials.Value.PasswordHash);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("short1A", false)]
    [InlineData("onlylower1", false)]
    [InlineData("ONLYUPPER1", false)]
    [InlineData("NoNumbers", false)]
    [InlineData("", false)]
    [InlineData("ValidPass1", true)]
    [InlineData("ValidPass1!", true)]
    public void PasswordValidator_ShouldRequireLengthUppercaseLowercaseAndNumber(string password, bool valid) =>
        Assert.Equal(valid, new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand(Password, password)).IsValid);

    [Fact]
    public void PasswordValidator_ShouldRejectReusingCurrentPassword() =>
        Assert.False(new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand(Password, Password)).IsValid);

    [Theory]
    [InlineData("", "Password1")]
    [InlineData("001A", "")]
    public void LoginValidator_ShouldRequireBothFields(string identification, string password) =>
        Assert.False(new LoginCommandValidator().Validate(new LoginCommand(identification, password)).IsValid);
}
