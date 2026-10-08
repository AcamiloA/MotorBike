namespace UniversityParking.Application.Common.Abstractions;

public interface IRequestContext
{
    string? IpAddress { get; }
    string? TraceId { get; }
}
