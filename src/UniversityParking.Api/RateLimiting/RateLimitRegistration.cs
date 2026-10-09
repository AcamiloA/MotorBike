using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UniversityParking.Api.ExceptionHandling;

namespace UniversityParking.Api.RateLimiting;

public static class RateLimitRegistration
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // The global limiter skips anonymous endpoints, so explicitly cover public reference data.
            options.AddPolicy(RateLimitPolicies.PublicCatalog, context => Window(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", RateLimitPolicies.GeneralPermitLimit));
            options.AddPolicy(RateLimitPolicies.StudentRegistration, context => Window(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", RateLimitPolicies.StudentRegistrationPermitLimit));
            options.AddPolicy(RateLimitPolicies.Login, context => Window(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", RateLimitPolicies.LoginPermitLimit));
            options.AddPolicy(RateLimitPolicies.Lookup, context => context.User.Identity?.IsAuthenticated == true
                ? Window(Actor(context), RateLimitPolicies.LookupPermitLimit) : RateLimitPartition.GetNoLimiter("anonymous"));
            options.AddPolicy(RateLimitPolicies.ParkingCommands, context => context.User.Identity?.IsAuthenticated == true
                ? Window(Actor(context), RateLimitPolicies.ParkingCommandsPermitLimit) : RateLimitPartition.GetNoLimiter("anonymous"));
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.User.Identity?.IsAuthenticated != true ||
                    context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null)
                    return RateLimitPartition.GetNoLimiter("unrestricted");
                return Window(Actor(context), RateLimitPolicies.GeneralPermitLimit);
            });
            options.OnRejected = (context, _) => new ValueTask(ProblemResponses.WriteAsync(context.HttpContext,
                429, "RATE_LIMIT_EXCEEDED", "Se excedió el límite de solicitudes. Intenta nuevamente en un minuto."));
        });
        return services;
    }

    private static string Actor(HttpContext context) => context.User.FindFirst("sub")?.Value ?? "anonymous";
    private static RateLimitPartition<string> Window(string key, int limit) => RateLimitPartition.GetFixedWindowLimiter(key,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst, AutoReplenishment = true
        });
}
