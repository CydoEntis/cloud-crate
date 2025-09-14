using CloudCrate.Application.Errors;

namespace CloudCrate.Application.Models;

public readonly struct Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private readonly Error _error;

    public static Result Success() => new(true, null!); 

    public static Result Failure(Error error) 
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }


    public Error GetError()
    {
        if (IsSuccess)
            throw new InvalidOperationException("Cannot get error from successful result");
        return _error;
    }

    public static implicit operator Result(Error error) => Failure(error);
}

public readonly struct Result<T>
{
    private Result(bool isSuccess, T value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private readonly T _value;
    private readonly Error _error;

    public static Result<T> Success(T value) => new(true, value, null!);

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default!, error);
    }

    public T GetValue()
    {
        if (IsFailure)
            throw new InvalidOperationException("Cannot get value from failed result");
        return _value;
    }


    public Error GetError()
    {
        if (IsSuccess)
            throw new InvalidOperationException("Cannot get error from successful result");
        return _error;
    }

    public bool TryGetValue(out T value)
    {
        if (IsSuccess)
        {
            value = _value;
            return true;
        }
        value = default!;
        return false;
    }

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}