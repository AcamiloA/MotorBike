using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Incidents;

public sealed class IncidentAttachment : Entity
{
    public Guid IncidentId { get; private set; }
    public string StorageKey { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IncidentAttachment(Guid incidentId, string storageKey, string originalFileName,
        string contentType, long sizeBytes, DateTimeOffset createdAt)
    {
        IncidentId = Guard.Id(incidentId, "incidente");
        StorageKey = FileMetadata.StorageKey(storageKey);
        OriginalFileName = Guard.Text(originalFileName, 255, "nombre del archivo");
        ContentType = FileMetadata.ContentType(contentType, allowPdf: true);
        SizeBytes = FileMetadata.Size(sizeBytes, 10 * 1024 * 1024);
        CreatedAt = Guard.Utc(createdAt);
    }
}
