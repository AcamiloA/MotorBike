using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Reporting;

namespace UniversityParking.Application.Tests.Reporting;

public sealed class ReportingDateTests
{
    // Use the same timezone contract with explicit local-midnight boundaries.
    private sealed class Zone : IParkingTimeZone
    {
        private readonly TimeZoneInfo value = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        public TimeOnly GetLocalTime(DateTimeOffset utc) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, value).DateTime);
        public DateTimeOffset GetUtcStartOfDay(DateOnly day) => new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), value));
    }
    private sealed class Clock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
    [Theory]
    [InlineData("2026-10-07T04:59:59Z", "2026-10-06T05:00:00Z")]
    [InlineData("2026-10-07T05:00:00Z", "2026-10-07T05:00:00Z")]
    public void TodayUsesBogotaMidnight(string now, string expected)
    {
        var range = ReportingDates.Today(new Clock(DateTimeOffset.Parse(now)), new Zone());
        Assert.Equal(DateTimeOffset.Parse(expected), range.From); Assert.Equal(range.From.AddDays(1), range.Until);
    }
    [Fact]
    public void RangeIncludesEntireLastLocalDayAndHandlesMaximumDate()
    {
        var range = ReportingDates.Range(new(2026, 10, 6), new(2026, 10, 7), new Zone());
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T05:00:00Z"), range.From);
        Assert.Equal(DateTimeOffset.Parse("2026-10-08T05:00:00Z"), range.Until);
        Assert.Null(ReportingDates.End(DateOnly.MaxValue, new Zone()));
    }
    [Fact]
    public void ReportDateValidatorsRejectMissingAndReversedRanges()
    {
        Assert.False(new GetDailyAccessReportQueryValidator().Validate(new GetDailyAccessReportQuery(default, default)).IsValid);
        Assert.False(new GetAccessByMemberTypeReportQueryValidator().Validate(new GetAccessByMemberTypeReportQuery(new(2026,10,7), new(2026,10,6))).IsValid);
        Assert.True(new GetAccessByVehicleTypeReportQueryValidator().Validate(new GetAccessByVehicleTypeReportQuery(new(2020,1,1), new(2026,10,7))).IsValid);
    }
}
