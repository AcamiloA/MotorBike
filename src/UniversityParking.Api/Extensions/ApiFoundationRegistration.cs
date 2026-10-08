using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Api.Middleware;
using UniversityParking.Api.OpenApi;
using UniversityParking.Api.RateLimiting;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.Extensions;

public static class ApiFoundationRegistration
{
    public static IServiceCollection AddApiFoundation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IRequestContext, RequestContext>();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status >= 500 ? ProblemResponses.InternalServerErrorCode : "VALIDATION_ERROR");
        });
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        }).ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var fields = context.ModelState.Where(pair => pair.Value?.Errors.Count > 0)
                    .ToDictionary(pair => pair.Key, _ => (IReadOnlyList<string>)new[] { "El campo es obligatorio o contiene un valor inválido." });
                var problem = ProblemResponses.Create(context.HttpContext, 400, "VALIDATION_ERROR", "Revisa los datos ingresados.", fields);
                var response = new BadRequestObjectResult(problem);
                response.ContentTypes.Add("application/problem+json");
                return response;
            };
        });
        services.AddApiRateLimiting();
        services.AddApiOpenApi();
        services.AddHealthChecks().AddCheck("api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
            .AddDbContextCheck<AppDbContext>("postgresql");
        return services;
    }
}
