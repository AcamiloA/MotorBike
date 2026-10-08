using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Users;

public sealed class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = Guard.Id(userId, "usuario");
        RoleId = Guard.Id(roleId, "rol");
    }
}
