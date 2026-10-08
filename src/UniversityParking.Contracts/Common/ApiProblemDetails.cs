namespace UniversityParking.Contracts.Common;

public sealed class ApiProblemDetails
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }
    public string Code { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
