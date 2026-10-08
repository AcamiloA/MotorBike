using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;
using UniversityParking.Infrastructure.Authentication;

namespace UniversityParking.Infrastructure.Tests.Authentication;

public sealed class AuthenticationTests
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void StandardHasher_ShouldSaltHashesAndVerifyOnlyCorrectPassword()
    {
        var hasher = new PasswordHasher();
        var first = hasher.Hash("Password123");
        var second = hasher.Hash("Password123");
        Assert.NotEqual("Password123", first);
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("Password123", first));
        Assert.False(hasher.Verify("Wrong123", first));
        Assert.False(hasher.Verify("Password123", string.Empty));
        Assert.False(hasher.Verify("Password123", "malformed hash"));
    }

    [Fact]
    public void Jwt_ShouldUseEightHourLifetime_AndOnlyMinimalIdentityClaims()
    {
        var options = new JwtOptions { Key = "TEST_ONLY_KEY_012345678901234567890123456789" };
        var clock = new FixedClock();
        var user = new TokenUser(Guid.NewGuid(), MemberType.STAFF, [RoleCodes.User, RoleCodes.Admin]);
        var service = new JwtTokenService(Options.Create(options), clock);
        var result = service.CreateAccessToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal(clock.UtcNow.AddHours(8), result.ExpiresAtUtc);
        Assert.Equal(clock.UtcNow.UtcDateTime, token.ValidFrom);
        Assert.Equal(result.ExpiresAtUtc.UtcDateTime, token.ValidTo);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal("STAFF", token.Claims.Single(x => x.Type == "member_type").Value);
        Assert.Equal(new[] { RoleCodes.Admin, RoleCodes.User }, token.Claims.Where(x => x.Type == "role").Select(x => x.Value).Order());
        Assert.DoesNotContain(token.Claims, x => x.Type is "identificationNumber" or "cardCode" or "password" or "fullName");
        var validation = new TokenValidationParameters
        {
            ValidIssuer = options.Issuer, ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            ValidateIssuerSigningKey = true, ValidateLifetime = false, RoleClaimType = "role"
        };
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(result.Token, validation, out _);
        Assert.True(principal.IsInRole(RoleCodes.Admin));
        Assert.False(principal.IsInRole(RoleCodes.Guard));
    }

    [Fact]
    public void MissingJwtKey_ShouldFailOptionsValidation()
    {
        var services = new ServiceCollection();
        services.AddJwtOptions(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }
}
