using UniversityParking.Domain.Common;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Domain.Tests.Universities;

public sealed class UniversityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorPreservesStableIdNormalizesCodeAndNameAndConvertsTimeToUtc()
    {
        var university = new University(UniversityIds.Etitc, " etitc ", " ETITC ", Now.ToOffset(TimeSpan.FromHours(-5)));
        Assert.Equal(UniversityIds.Etitc, university.Id);
        Assert.Equal("ETITC", university.Code);
        Assert.Equal("ETITC", university.Name);
        Assert.True(university.IsActive);
        Assert.Equal(Now, university.CreatedAt);
        Assert.Equal(TimeSpan.Zero, university.CreatedAt.Offset);
        Assert.Equal(university.CreatedAt, university.UpdatedAt);
    }

    [Theory]
    [InlineData(null, "ETITC")]
    [InlineData("", "ETITC")]
    [InlineData(" ", "ETITC")]
    [InlineData("ETITC", null)]
    [InlineData("ETITC", "")]
    [InlineData("ETITC", " ")]
    public void ConstructorRejectsMissingCodeOrName(string? code, string? name)
        => AssertValidation(() => new University(UniversityIds.Etitc, code!, name!, Now));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConstructorRejectsTextBeyondMaximumLength(bool code)
        => AssertValidation(() => new University(UniversityIds.Etitc,
            code ? new string('A', 21) : "ETITC", code ? "ETITC" : new string('A', 201), Now));

    [Fact]
    public void ConstructorAcceptsMaximumLengths()
    {
        var university = new University(UniversityIds.Etitc, new string('A', 20), new string('A', 200), Now);
        Assert.Equal(20, university.Code.Length);
        Assert.Equal(200, university.Name.Length);
    }

    [Fact]
    public void ConstructorRejectsEmptyIdAndMissingTimestamp()
    {
        AssertValidation(() => new University(Guid.Empty, "ETITC", "ETITC", Now));
        AssertValidation(() => new University(UniversityIds.Etitc, "ETITC", "ETITC", default));
    }

    [Fact]
    public void StateChangesPreserveIdentityCodeAndCreatedAtAndAreIdempotent()
    {
        var university = new University(UniversityIds.Cmc, "CMC", "Colegio Mayor de Cundinamarca", Now);
        university.Deactivate(Now.AddHours(1));
        Assert.False(university.IsActive);
        Assert.Equal(Now.AddHours(1), university.UpdatedAt);
        university.Deactivate(Now.AddHours(2));
        Assert.Equal(Now.AddHours(1), university.UpdatedAt);
        university.Activate(Now.AddHours(3));
        Assert.True(university.IsActive);
        Assert.Equal(Now.AddHours(3), university.UpdatedAt);
        Assert.Equal(Now, university.CreatedAt);
        Assert.Equal(UniversityIds.Cmc, university.Id);
        Assert.Equal("CMC", university.Code);
    }

    [Fact]
    public void StateChangeRejectsEarlierTimestampWithoutChangingState()
    {
        var university = new University(UniversityIds.Upn, "UPN", "U. Pedagógica", Now);
        AssertValidation(() => university.Deactivate(Now.AddMinutes(-1)));
        Assert.True(university.IsActive);
        Assert.Equal(Now, university.UpdatedAt);
    }

    [Fact]
    public void ReferenceIdentitiesAreDistinctAndReproducible()
    {
        Assert.Equal(new Guid("a1100000-0000-4000-8000-000000000001"), UniversityIds.Etitc);
        Assert.Equal(new Guid("a1100000-0000-4000-8000-000000000002"), UniversityIds.Cmc);
        Assert.Equal(new Guid("a1100000-0000-4000-8000-000000000003"), UniversityIds.Upn);
        Assert.Equal(3, new[] { UniversityIds.Etitc, UniversityIds.Cmc, UniversityIds.Upn }.Distinct().Count());
    }

    private static void AssertValidation(Action action)
        => Assert.Equal("VALIDATION_ERROR", Assert.Throws<DomainException>(action).Code);
}
