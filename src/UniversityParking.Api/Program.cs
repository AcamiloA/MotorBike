using UniversityParking.Api.Authentication;
using UniversityParking.Application;
using UniversityParking.Infrastructure;
using UniversityParking.Api.Extensions;
using UniversityParking.Api.ExceptionHandling;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (port is not null)
{
    if (!int.TryParse(port, out var number) || number is < 1 or > 65535)
        throw new InvalidOperationException("PORT debe ser un puerto válido.");
    builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication();
builder.Services.AddApiFoundation();

var app = builder.Build();

await app.ApplyStartupMigrationsAsync();
await app.ApplyStartupSeedAsync();

app.UseExceptionHandler();
app.UseStatusCodePages(context => ProblemResponses.WriteStatusAsync(context.HttpContext));
if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "MOTOBIKE PARK API v1"));
}
if (app.Configuration.GetValue("HttpsRedirection:Enabled", true))
    app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => report.Status == HealthStatus.Healthy
        ? Results.Json(new { status = "Healthy" }).ExecuteAsync(context)
        : ProblemResponses.WriteAsync(context, 503, "SERVICE_UNAVAILABLE", "El servicio no está disponible.")
}).AllowAnonymous().DisableRateLimiting();

app.Run();

public partial class Program;
