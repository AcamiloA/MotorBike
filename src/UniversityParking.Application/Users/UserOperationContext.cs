using System.Text.Json;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Users;

public sealed class UserOperationContext(ICurrentUser actor, IUserRepository users, IRoleRepository roles,
    IAuditLogRepository audits, IClock clock, IRequestContext requestContext)
{
    public DateTimeOffset UtcNow => clock.UtcNow;
    public Guid? ActorId => actor.UserId;
    public async Task<Error?> CheckAccessAsync(bool administrator, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated || actor.UserId is not { } id) return AuthErrors.InvalidCredentials;
        if (administrator && !actor.IsInRole(RoleCodes.Admin)) return CommonErrors.Forbidden;
        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null) return UserErrors.NotFound;
        if (user.Status != UserStatus.ACTIVE) return UserErrors.Inactive;
        if (administrator && !(await roles.GetCodesByUserIdAsync(id, cancellationToken)).Contains(RoleCodes.Admin))
            return CommonErrors.Forbidden;
        return null;
    }
    public Task AuditAsync(string action, Guid userId, object? oldValues, object? newValues, CancellationToken cancellationToken) =>
        audits.AddAsync(new AuditLog(actor.UserId, action, "User", userId, clock.UtcNow,
            oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            newValues is null ? null : JsonSerializer.Serialize(newValues), requestContext.IpAddress, requestContext.TraceId), cancellationToken);
    public static object Snapshot(User user, string universityName) => new
    {
        user.FullName, user.UniversityId, UniversityName = universityName, user.Career, user.MemberType, CardCode = user.CardCode.Value, user.Status
    };
}
