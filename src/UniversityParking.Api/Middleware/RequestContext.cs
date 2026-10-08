using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Api.Middleware;

public sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? TraceId => accessor.HttpContext?.TraceIdentifier;
}
