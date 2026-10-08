using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Tests.Auditing;

public sealed class AuditLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Audit_ShouldPreserveActorEntityAndOldNewValues()
    {
        var actor = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var audit = new AuditLog(actor, "VEHICLE_IDENTIFIER_CORRECTED", "Vehicle", entity, Now,
            "{\"plate\":\"ABC123\"}", "{\"plate\":\"DEF456\"}", "127.0.0.1", "trace-123");
        Assert.Equal(actor, audit.ActorUserId);
        Assert.Equal(entity, audit.EntityId);
        Assert.Equal("{\"plate\":\"ABC123\"}", audit.OldValues);
        Assert.Equal("{\"plate\":\"DEF456\"}", audit.NewValues);
        Assert.Equal("trace-123", audit.TraceId);
    }

    [Fact]
    public void TechnicalAudit_ShouldAllowMissingActorAndValues()
    {
        var audit = new AuditLog(null, "SEED", "Role", null, Now);
        Assert.Null(audit.ActorUserId);
        Assert.Null(audit.EntityId);
        Assert.Null(audit.OldValues);
        Assert.Null(audit.NewValues);
    }

    [Fact]
    public void Audit_ShouldRejectMalformedJson() => Assert.Throws<DomainException>(() =>
        new AuditLog(null, "USER_UPDATED", "User", Guid.NewGuid(), Now, newValues: "{invalid}"));
}
