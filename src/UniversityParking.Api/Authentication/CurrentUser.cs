using System.Security.Claims;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) && id != Guid.Empty ? id : null;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && UserId.HasValue;
    public IReadOnlyCollection<string> Roles => Principal?.FindAll("role").Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray() ?? [];
    public MemberType? MemberType => Enum.TryParse<MemberType>(Principal?.FindFirstValue("member_type"), out var memberType)
        && Enum.IsDefined(memberType) ? memberType : null;
    public bool IsInRole(string roleCode) => IsAuthenticated && Roles.Contains(roleCode, StringComparer.Ordinal);
}
