using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Auditing;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(AppDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken) => await context.AuditLogs.AddAsync(auditLog, cancellationToken);
}
