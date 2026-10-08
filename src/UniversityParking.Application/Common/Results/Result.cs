namespace UniversityParking.Application.Common.Results;

public interface IResult
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
    Error? Error { get; }
}

public interface IResult<TSelf> : IResult where TSelf : IResult<TSelf>
{
    static abstract TSelf Failure(Error error);
}

public sealed class Result : IResult<Result>
{
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    private Result(Error? error) => Error = error;

    public static Result Success() => new(null);
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
}

public sealed class Result<T> : IResult<Result<T>>
{
    private readonly T? value;
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }
    public T Value => IsSuccess ? value! : throw new InvalidOperationException("Un resultado fallido no contiene un valor.");

    private Result(T? value, Error? error)
    {
        this.value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default, error);
    }
}
