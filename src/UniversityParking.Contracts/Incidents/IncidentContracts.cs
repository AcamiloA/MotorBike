using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.Incidents;

public enum IncidentKind { DAMAGE, ACCIDENT, SECURITY, DOCUMENT, BEHAVIOR, OTHER }
public enum IncidentState { OPEN, RESOLVED, CANCELLED }
public sealed record IncidentCreatedResponse(Guid Id);
public sealed record IncidentResponse(Guid Id, Guid ParkingLotId, Guid? UserId, Guid? VehicleId, Guid? ParkingMovementId,
    Guid ReportedBy, string Type, string Description, string Status, DateTimeOffset OccurredAt,
    string? Resolution, Guid? ResolvedBy, DateTimeOffset? ResolvedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record IncidentAttachmentResponse(Guid Id, string OriginalFileName, string ContentType, long SizeBytes, string ContentUrl);
public sealed record IncidentDetailResponse(IncidentResponse Incident, IReadOnlyList<IncidentAttachmentResponse> Attachments);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResolveIncidentRequest(string Resolution);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelIncidentRequest(string? Reason = null);
