using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
