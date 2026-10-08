using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UniversityParking.Api.Authentication;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Auth;

public sealed class CurrentUserTests
{
    [Fact]
    public void CurrentUser_ShouldReadOnlyAuthenticatedActorAndExplicitRoleClaims()
    {
        var id = Guid.NewGuid();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", id.ToString()), new Claim("role", RoleCodes.User), new Claim("role", RoleCodes.Admin), new Claim("member_type", "STAFF")],
            "Bearer", "sub", "role"));
        var current = new CurrentUser(accessor);
        Assert.True(current.IsAuthenticated);
        Assert.Equal(id, current.UserId);
        Assert.Equal(MemberType.STAFF, current.MemberType);
        Assert.True(current.IsInRole(RoleCodes.Admin));
        Assert.False(current.IsInRole(RoleCodes.Guard));
    }

    [Fact]
    public void CurrentUser_ShouldNotTreatMissingOrInvalidSubjectAsAuthenticated()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "invalid")], "Bearer"));
        var current = new CurrentUser(accessor);
        Assert.False(current.IsAuthenticated);
        Assert.Null(current.UserId);
        Assert.False(current.IsInRole(RoleCodes.Guard));
    }
}
