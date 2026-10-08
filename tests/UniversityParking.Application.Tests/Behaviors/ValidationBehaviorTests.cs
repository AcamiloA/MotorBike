using FluentValidation;
using MediatR;
using UniversityParking.Application.Common.Behaviors;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private sealed record Input(string FullName, int Age);

    [Fact]
    public async Task InvalidInput_ShouldAggregateValidators_AndPreventHandlerExecution()
    {
        var nameValidator = new InlineValidator<Input>();
        nameValidator.RuleFor(x => x.FullName).NotEmpty().WithMessage("El nombre es obligatorio.");
        var ageValidator = new InlineValidator<Input>();
        ageValidator.RuleFor(x => x.Age).GreaterThan(0).WithMessage("El valor debe ser positivo.");
        var behavior = new ValidationBehavior<Input, Result<string>>([nameValidator, ageValidator]);
        var executed = false;
        RequestHandlerDelegate<Result<string>> next = _ =>
        {
            executed = true;
            return Task.FromResult(Result<string>.Success("OK"));
        };

        var result = await behavior.Handle(new Input("", 0), next, CancellationToken.None);

        Assert.False(executed);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
        Assert.Equal("El nombre es obligatorio.", Assert.Single(result.Error.ValidationErrors["FullName"]));
        Assert.Equal("El valor debe ser positivo.", Assert.Single(result.Error.ValidationErrors["Age"]));
    }

    [Fact]
    public async Task ValidInput_ShouldContinueToHandler()
    {
        var validator = new InlineValidator<Input>();
        validator.RuleFor(x => x.FullName).NotEmpty();
        var behavior = new ValidationBehavior<Input, Result<string>>([validator]);
        var result = await behavior.Handle(new Input("Camilo", 1), _ => Task.FromResult(Result<string>.Success("OK")), CancellationToken.None);
        Assert.Equal("OK", result.Value);
    }

    [Fact]
    public async Task NoValidators_ShouldPreserveHandlerResult()
    {
        var expected = Result.Success();
        var behavior = new ValidationBehavior<Input, Result>([]);
        var result = await behavior.Handle(new Input("", 0), _ => Task.FromResult(expected), CancellationToken.None);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task AsyncValidation_ShouldReceiveCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var received = CancellationToken.None;
        var validator = new InlineValidator<Input>();
        validator.RuleFor(x => x.FullName).MustAsync((_, token) =>
        {
            received = token;
            return Task.FromResult(false);
        });
        var behavior = new ValidationBehavior<Input, Result>([validator]);
        var result = await behavior.Handle(new Input("Nombre", 1), _ => Task.FromResult(Result.Success()), source.Token);
        Assert.Equal(source.Token, received);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Cancellation_ShouldPropagate_WithoutExecutingHandler()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var executed = false;
        var behavior = new ValidationBehavior<Input, Result>([]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(new Input("", 0), _ =>
        {
            executed = true;
            return Task.FromResult(Result.Success());
        }, source.Token));
        Assert.False(executed);
    }

    [Fact]
    public async Task UnexpectedValidationFailure_ShouldPropagate()
    {
        var validator = new InlineValidator<Input>();
        validator.RuleFor(x => x.FullName).MustAsync((_, _) => throw new InvalidOperationException("Technical failure"));
        var behavior = new ValidationBehavior<Input, Result>([validator]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(new Input("Nombre", 1),
            _ => Task.FromResult(Result.Success()), CancellationToken.None));
    }
}
