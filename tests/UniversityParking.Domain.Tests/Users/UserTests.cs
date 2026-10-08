using UniversityParking.Domain.Common;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Domain.Tests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static User Create(MemberType type = MemberType.STUDENT, string? career = "Ingeniería") =>
        new(new IdentificationNumber(" 00-A12 "), " Camilo ", UniversityParking.Domain.Universities.UniversityIds.Etitc, career, type, new CardCode(" QR-abc "), Now);

    [Fact]
    public void IdentificationNumber_ShouldPreserveTextAndLeadingZeros_WhenNormalized()
    {
        var number = new IdentificationNumber(" 001A-02 ");
        Assert.Equal("001A-02", number.Value);
        Assert.Equal(new IdentificationNumber("001A-02"), number);
        Assert.NotEqual(new IdentificationNumber("001a-02"), number);
    }

    [Fact]
    public void CardCode_ShouldOnlyTrimOuterWhitespace()
    {
        var code = new CardCode(" https://example/Ab C-12 ");
        Assert.Equal("https://example/Ab C-12", code.Value);
        Assert.Equal(new CardCode("https://example/Ab C-12"), code);
        Assert.NotEqual(new CardCode("https://example/ab C-12"), code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Identifiers_ShouldRejectEmptyValues(string value)
    {
        Assert.Equal("VALIDATION_ERROR", Assert.Throws<DomainException>(() => new IdentificationNumber(value)).Code);
        Assert.Throws<DomainException>(() => new CardCode(value));
    }

    [Fact]
    public void User_ShouldNormalizeRequiredFields_AndStartActive()
    {
        var user = Create();
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Camilo", user.FullName);
        Assert.Equal(UniversityParking.Domain.Universities.UniversityIds.Etitc, user.UniversityId);
        Assert.Equal(UserStatus.ACTIVE, user.Status);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal(Now, user.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Student_ShouldRequireCareer(string? career) =>
        Assert.Throws<DomainException>(() => Create(career: career));

    [Theory]
    [InlineData(MemberType.TEACHER)]
    [InlineData(MemberType.STAFF)]
    public void OtherMembers_ShouldAllowMissingCareer(MemberType type) => Assert.Null(Create(type, null).Career);

    [Fact]
    public void UpdateProfile_ShouldPreserveInstitutionAndIdentity()
    {
        var user = Create();
        user.UpdateProfile(" Andrés ", " Sistemas ", Now.AddMinutes(1));
        Assert.Equal("Andrés", user.FullName);
        Assert.Equal("Sistemas", user.Career);
        Assert.Equal(UniversityParking.Domain.Universities.UniversityIds.Etitc, user.UniversityId);
        Assert.Equal("00-A12", user.IdentificationNumber.Value);
        Assert.Equal(MemberType.STUDENT, user.MemberType);
        Assert.Equal("QR-abc", user.CardCode.Value);
    }

    [Fact]
    public void FailedUpdate_ShouldLeavePreviousUserDataUnchanged()
    {
        var user = Create();
        Assert.Throws<DomainException>(() => user.Update("Otro", UniversityParking.Domain.Universities.UniversityIds.Cmc, null, MemberType.STUDENT, new CardCode("new"), Now.AddMinutes(1)));
        Assert.Equal("Camilo", user.FullName);
        Assert.Equal(UniversityParking.Domain.Universities.UniversityIds.Etitc, user.UniversityId);
        Assert.Equal("QR-abc", user.CardCode.Value);
        Assert.Equal(Now, user.UpdatedAt);
    }

    [Fact]
    public void StatusChanges_ShouldBeIdempotent_AndPreserveUserId()
    {
        var user = Create();
        var id = user.Id;
        user.Deactivate(Now.AddMinutes(1));
        user.Deactivate(Now.AddMinutes(2));
        Assert.Equal(UserStatus.INACTIVE, user.Status);
        Assert.Equal(Now.AddMinutes(1), user.UpdatedAt);
        user.Activate(Now.AddMinutes(3));
        user.Activate(Now.AddMinutes(4));
        Assert.Equal(UserStatus.ACTIVE, user.Status);
        Assert.Equal(Now.AddMinutes(3), user.UpdatedAt);
        Assert.Equal(id, user.Id);
    }

    [Fact]
    public void UniversityReferenceRejectsEmptyGuidAndPreservesDataOnInvalidUpdate()
    {
        Assert.Throws<DomainException>(() => new User(new IdentificationNumber("id"), "Nombre", Guid.Empty,
            null, MemberType.STAFF, new CardCode("card"), Now));
        var user = Create();
        Assert.Throws<DomainException>(() => user.Update("Otro", Guid.Empty, "Ingeniería", MemberType.STUDENT,
            new CardCode("new"), Now.AddMinutes(1)));
        Assert.Equal("Camilo", user.FullName);
        Assert.Equal(UniversityParking.Domain.Universities.UniversityIds.Etitc, user.UniversityId);
    }

    [Theory]
    [InlineData("USER")]
    [InlineData("GUARD")]
    [InlineData("ADMIN")]
    public void Role_ShouldAcceptOnlyDefinedCapabilities(string code) => Assert.Equal(code, new Role(code).Code);

    [Theory]
    [InlineData("ROOT")]
    [InlineData("SUPERADMIN")]
    [InlineData("admin")]
    public void Role_ShouldRejectUndefinedCapabilities(string code) => Assert.Throws<DomainException>(() => new Role(code));

    [Fact]
    public void Credential_ShouldStoreOnlyHashAndUpdateChangeTimestamp()
    {
        var credential = new UserCredential(Guid.NewGuid(), "opaque-hash", Now);
        credential.ChangePasswordHash("new-opaque-hash", Now.AddMinutes(1));
        Assert.Equal("new-opaque-hash", credential.PasswordHash);
        Assert.Equal(Now.AddMinutes(1), credential.PasswordChangedAt);
        Assert.Throws<DomainException>(() => credential.ChangePasswordHash(" ", Now.AddMinutes(2)));
        Assert.Equal("new-opaque-hash", credential.PasswordHash);
    }

    [Fact]
    public void UserRole_ShouldRejectEmptyRelationshipIds()
    {
        Assert.Throws<DomainException>(() => new UserRole(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new UserRole(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void User_ShouldRejectInvalidMemberType_AndExcessiveFieldLengths()
    {
        Assert.Throws<DomainException>(() => Create((MemberType)99));
        Assert.Throws<DomainException>(() => new IdentificationNumber(new string('a', 51)));
        Assert.Throws<DomainException>(() => new CardCode(new string('a', 151)));
        var user = Create();
        Assert.Throws<DomainException>(() => user.UpdateProfile(new string('a', 201), "Sistemas", Now));
    }
}
