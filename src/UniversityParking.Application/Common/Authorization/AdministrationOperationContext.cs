using System.Text.Json;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Common.Authorization;

public sealed class AdministrationOperationContext(ICurrentUser actor, IUserRepository users, IRoleRepository roles,
    IAuditLogRepository audits, IClock clock, IRequestContext request)
{
    public DateTimeOffset UtcNow => clock.UtcNow;
    public async Task LockActorAsync(CancellationToken cancellationToken) =>
        await users.GetByIdForUpdateAsync(actor.UserId!.Value, cancellationToken);
    public async Task<Error?> CheckAccessAsync(IReadOnlyCollection<string> allowedRoles, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated || actor.UserId is not { } id) return AuthErrors.InvalidCredentials;
        if (allowedRoles.Count > 0 && !allowedRoles.Any(actor.IsInRole)) return CommonErrors.Forbidden;
        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null) return UserErrors.NotFound;
        if (user.Status != UserStatus.ACTIVE) return UserErrors.Inactive;
        if (allowedRoles.Count > 0)
        {
            var currentRoles = await roles.GetCodesByUserIdAsync(id, cancellationToken);
            if (!allowedRoles.Any(code => actor.IsInRole(code) && currentRoles.Contains(code))) return CommonErrors.Forbidden;
        }
        return null;
    }
    public Task AuditAsync(string action, string entityType, Guid entityId, object? before, object? after, CancellationToken cancellationToken) =>
        audits.AddAsync(new AuditLog(actor.UserId, action, entityType, entityId, clock.UtcNow,
            before is null ? null : JsonSerializer.Serialize(before), after is null ? null : JsonSerializer.Serialize(after), request.IpAddress, request.TraceId), cancellationToken);
}
