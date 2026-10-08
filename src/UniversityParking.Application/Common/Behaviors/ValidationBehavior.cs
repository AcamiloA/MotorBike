using FluentValidation;
using MediatR;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            failures.AddRange(validation.Errors);
        }

        if (failures.Count == 0) return await next(cancellationToken);

        var errors = failures.GroupBy(f => f.PropertyName).ToDictionary(
            group => group.Key, group => (IReadOnlyList<string>)group.Select(f => f.ErrorMessage).Distinct().ToArray());
        return TResponse.Failure(CommonErrors.Validation(errors));
    }
}
