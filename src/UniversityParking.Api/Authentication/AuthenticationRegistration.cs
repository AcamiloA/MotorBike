using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniversityParking.Api.Authorization;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;
using UniversityParking.Infrastructure.Authentication;
using UniversityParking.Api.ExceptionHandling;

namespace UniversityParking.Api.Authentication;

public static class AuthenticationRegistration
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RoleClaimType = "role",
                    NameClaimType = "sub",
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var actor = context.Principal?.FindFirst("sub")?.Value;
                        if (!Guid.TryParse(actor, out var id) || id == Guid.Empty)
                            context.Fail("El token no contiene un usuario válido.");
                        else
                        {
                            var users=context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                            var credentials=context.HttpContext.RequestServices.GetRequiredService<IUserCredentialRepository>();
                            var user=await users.GetByIdAsync(id,context.HttpContext.RequestAborted);
                            var credential=await credentials.GetByUserIdAsync(id,context.HttpContext.RequestAborted);
                            var stamp=context.Principal?.FindFirst("credential_version")?.Value;
                            if(user?.MustChangePassword==true || credential is null ||
                                (stamp is null ? credential.SecurityStamp!=Guid.Empty : !Guid.TryParse(stamp,out var changed) || changed!=credential.SecurityStamp))
                                context.Fail("La sesión requiere autenticación nuevamente.");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await ProblemResponses.WriteAsync(context.HttpContext, 401, "AUTH_INVALID_CREDENTIALS", "Se requiere una sesión válida.");
                    },
                    OnForbidden = context => ProblemResponses.WriteAsync(context.HttpContext, 403, "FORBIDDEN", "No tienes permisos para realizar esta acción.")
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.Authenticated, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(PolicyNames.Guard, policy => policy.RequireAuthenticatedUser().RequireRole(RoleCodes.Guard));
            options.AddPolicy(PolicyNames.Admin, policy => policy.RequireAuthenticatedUser().RequireRole(RoleCodes.Admin));
            options.AddPolicy(PolicyNames.GuardOrAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(RoleCodes.Guard, RoleCodes.Admin));
        });
        return services;
    }

}
