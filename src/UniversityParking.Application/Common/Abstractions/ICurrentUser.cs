using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
    MemberType? MemberType { get; }
    bool IsInRole(string roleCode);
}
