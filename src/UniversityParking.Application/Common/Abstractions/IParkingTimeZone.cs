namespace UniversityParking.Application.Common.Abstractions;

public interface IParkingTimeZone
{
    TimeOnly GetLocalTime(DateTimeOffset utc);
    DateTimeOffset GetUtcStartOfDay(DateOnly localDate);
}
