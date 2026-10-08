using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Behaviors;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Tests.Behaviors;

public sealed class LoggingBehaviorTests
{
    private sealed record SensitiveRequest(string Password, string CurrentPassword, string NewPassword);
    private sealed class RequestContext : IRequestContext
    {
        public string? TraceId => "trace-123";
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add(formatter(state, exception));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Logging_ShouldReportTechnicalResultAndTrace_WithoutPasswords(bool success)
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveRequest, Result>>();
        var behavior = new LoggingBehavior<SensitiveRequest, Result>(logger, new RequestContext());
        var request = new SensitiveRequest("first-secret", "current-secret", "new-secret");
        var expected = success ? Result.Success() : Result.Failure(CommonErrors.Forbidden);
        var result = await behavior.Handle(request, _ => Task.FromResult(expected), CancellationToken.None);
        Assert.Same(expected, result);
        var output = string.Join("\n", logger.Entries);
        Assert.Contains("SensitiveRequest", output);
        Assert.Contains("trace-123", output);
        Assert.Contains($"Success {success}", output);
        Assert.DoesNotContain(request.Password, output);
        Assert.DoesNotContain(request.CurrentPassword, output);
        Assert.DoesNotContain(request.NewPassword, output);
    }

    [Fact]
    public async Task UnexpectedException_ShouldPropagate_WithoutLoggingSensitiveExceptionMessage()
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveRequest, Result>>();
        var behavior = new LoggingBehavior<SensitiveRequest, Result>(logger);
        var exception = new InvalidOperationException("connection-string-secret");
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new SensitiveRequest("secret", "secret", "secret"), _ => throw exception, CancellationToken.None));
        Assert.Same(exception, caught);
        Assert.Contains(logger.Entries, entry => entry.Contains("failed"));
        Assert.DoesNotContain("connection-string-secret", string.Join("\n", logger.Entries));
    }
}
