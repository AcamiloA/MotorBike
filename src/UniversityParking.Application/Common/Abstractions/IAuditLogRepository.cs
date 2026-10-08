using UniversityParking.Domain.Auditing;

namespace UniversityParking.Application.Common.Abstractions;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken);
}
