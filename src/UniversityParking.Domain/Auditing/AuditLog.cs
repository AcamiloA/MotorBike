using System.Text.Json;
using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Auditing;

public sealed class AuditLog : Entity
{
    public Guid? ActorUserId { get; }
    public string Action { get; }
    public string EntityType { get; }
    public Guid? EntityId { get; }
    public string? OldValues { get; }
    public string? NewValues { get; }
    public string? IpAddress { get; }
    public string? TraceId { get; }
    public DateTimeOffset CreatedAt { get; }

    public AuditLog(Guid? actorUserId, string action, string entityType, Guid? entityId,
        DateTimeOffset createdAt, string? oldValues = null, string? newValues = null,
        string? ipAddress = null, string? traceId = null)
    {
        ActorUserId = Guard.OptionalId(actorUserId, "actor");
        Action = Guard.Text(action, 100, "acción");
        EntityType = Guard.Text(entityType, 100, "entidad");
        EntityId = Guard.OptionalId(entityId, "entidad");
        OldValues = ValidateJson(oldValues);
        NewValues = ValidateJson(newValues);
        IpAddress = Guard.OptionalText(ipAddress, 64, "IP");
        TraceId = Guard.OptionalText(traceId, 100, "traza");
        CreatedAt = Guard.Utc(createdAt);
    }

    private static string? ValidateJson(string? value)
    {
        if (value is null) return null;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            throw new DomainException("VALIDATION_ERROR", "Los valores de auditoría deben ser JSON válido.");
        }
    }
}
