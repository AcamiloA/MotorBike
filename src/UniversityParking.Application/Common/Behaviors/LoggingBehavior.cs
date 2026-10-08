using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    IRequestContext? requestContext = null) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        var traceId = requestContext?.TraceId;
        var started = Stopwatch.GetTimestamp();
        logger.LogInformation("Application request {RequestType} started. TraceId {TraceId}", requestType, traceId);
        try
        {
            var response = await next(cancellationToken);
            logger.LogInformation("Application request {RequestType} finished in {ElapsedMilliseconds} ms. Success {IsSuccess}. TraceId {TraceId}",
                requestType, Stopwatch.GetElapsedTime(started).TotalMilliseconds, response.IsSuccess, traceId);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Application request {RequestType} cancelled after {ElapsedMilliseconds} ms. TraceId {TraceId}",
                requestType, Stopwatch.GetElapsedTime(started).TotalMilliseconds, traceId);
            throw;
        }
        catch (Exception)
        {
            // Exception details belong to the API boundary; request values never enter these logs.
            logger.LogError("Application request {RequestType} failed after {ElapsedMilliseconds} ms. TraceId {TraceId}",
                requestType, Stopwatch.GetElapsedTime(started).TotalMilliseconds, traceId);
            throw;
        }
    }
}
