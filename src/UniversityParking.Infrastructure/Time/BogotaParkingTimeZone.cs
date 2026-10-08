using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Time;

public sealed class ParkingTimeOptions { public string TimeZone { get; set; } = "America/Bogota"; }
public sealed class BogotaParkingTimeZone : IParkingTimeZone
{
    private readonly TimeZoneInfo zone;
    public BogotaParkingTimeZone(IOptions<ParkingTimeOptions> options) => zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    public TimeOnly GetLocalTime(DateTimeOffset utc) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, zone).DateTime);
    public DateTimeOffset GetUtcStartOfDay(DateOnly localDate) =>
        new(TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone));
}
