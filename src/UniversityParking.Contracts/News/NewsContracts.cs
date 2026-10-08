using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.News;

public enum NewsState { DRAFT, PUBLISHED, ARCHIVED }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateNewsRequest(string Title, string Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateNewsRequest(string Title, string Content);
public sealed record NewsCreatedResponse(Guid Id);
public sealed record NewsResponse(Guid Id, string Title, string Content, string Status, DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt, Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
