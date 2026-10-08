using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Tests.AcademicPeriods;

public sealed class AcademicPeriodTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static AcademicPeriod Create() => new(" 2026-2 ", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 20), Now);

    [Fact]
    public void Period_ShouldStartPlanned_AndFollowValidTransitions()
    {
        var period = Create();
        Assert.Equal("2026-2", period.Name);
        Assert.Equal(AcademicPeriodStatus.PLANNED, period.Status);
        period.Activate();
        period.Activate();
        Assert.Equal(AcademicPeriodStatus.ACTIVE, period.Status);
        period.Close();
        period.Close();
        Assert.Equal(AcademicPeriodStatus.CLOSED, period.Status);
    }

    [Fact]
    public void ClosedPeriod_ShouldRejectReactivation()
    {
        var period = Create();
        period.Activate();
        period.Close();
        Assert.Throws<DomainException>(() => period.Activate());
        Assert.Equal(AcademicPeriodStatus.CLOSED, period.Status);
    }

    [Fact]
    public void PlannedPeriod_ShouldRejectClosing()
    {
        var period = Create();
        Assert.Equal("ACADEMIC_PERIOD_NOT_ACTIVE", Assert.Throws<DomainException>(() => period.Close()).Code);
        Assert.Equal(AcademicPeriodStatus.PLANNED, period.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Period_ShouldRejectNonIncreasingDates(int days)
    {
        var start = new DateOnly(2026, 7, 1);
        Assert.Throws<DomainException>(() => new AcademicPeriod("2026-2", start, start.AddDays(days), Now));
    }

    [Fact]
    public void ClosingPeriod_ShouldNotMutateHistoricalRegistration()
    {
        var period = Create();
        period.Activate();
        var registration = new UniversityParking.Domain.Vehicles.VehicleRegistration(Guid.NewGuid(), Guid.NewGuid(), period.Id, Now);
        period.Close();
        Assert.Equal(UniversityParking.Domain.Vehicles.VehicleRegistrationStatus.ACTIVE, registration.Status);
    }
}
