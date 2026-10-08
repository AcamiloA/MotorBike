using Microsoft.Extensions.Options;
using UniversityParking.Infrastructure.Time;

namespace UniversityParking.Infrastructure.Tests.Time;

public sealed class ParkingTimeZoneTests
{
    private readonly BogotaParkingTimeZone zone = new(Options.Create(new ParkingTimeOptions()));
    [Theory]
    [InlineData(11, 0, 0, 6, 0, 0)]
    [InlineData(10, 59, 59, 5, 59, 59)]
    [InlineData(3, 0, 0, 22, 0, 0)]
    [InlineData(2, 59, 59, 21, 59, 59)]
    public void UtcConvertsToBogotaExactly(int hour, int minute, int second, int localHour, int localMinute, int localSecond) =>
        Assert.Equal(new TimeOnly(localHour, localMinute, localSecond), zone.GetLocalTime(new(2026, 10, 7, hour, minute, second, TimeSpan.Zero)));
    [Fact]
    public void LocalDateStartsAtFiveUtc() => Assert.Equal(new DateTimeOffset(2026, 10, 7, 5, 0, 0, TimeSpan.Zero), zone.GetUtcStartOfDay(new(2026, 10, 7)));
}
