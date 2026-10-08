using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Api.E2E.Tests.Foundation;

public sealed class ProblemMappingTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void Mapper_ShouldPreserveHttpSemanticsAndTrace(ErrorType type, int status)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-test" };
        context.Request.Path = "/api/v1/auth/login";
        var result = ProblemResponses.Map(context, new Error("TEST_CODE", "Mensaje público", type));
        Assert.Equal(status, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(status, problem.Status);
        Assert.Equal("trace-test", problem.Extensions["traceId"]);
        Assert.Equal("/api/v1/auth/login", problem.Instance);
        Assert.Contains("application/problem+json", result.ContentTypes);
    }

    [Fact]
    public void UnexpectedResult_ShouldNotExposeTechnicalMessageOrValidationData()
    {
        var error = new Error("INTERNAL_CODE", "TEST_SECRET", ErrorType.Unexpected,
            new Dictionary<string, IReadOnlyList<string>> { ["InternalField"] = new[] { "TEST_SECRET" } });
        var problem = Assert.IsType<ProblemDetails>(ProblemResponses.Map(new DefaultHttpContext(), error).Value);
        Assert.DoesNotContain("TEST_SECRET", problem.Detail!);
        Assert.Equal("INTERNAL_SERVER_ERROR", problem.Extensions["code"]);
        Assert.False(problem.Extensions.ContainsKey("errors"));
    }

    [Theory]
    [InlineData("FullName", "fullName")]
    [InlineData("$.Password", "password")]
    [InlineData("Documents[0].Type", "documents[0].type")]
    [InlineData("$", "request")]
    public void ValidationFieldNames_ShouldMatchJsonContracts(string input, string expected) =>
        Assert.Equal(expected, ProblemResponses.FieldName(input));
}
