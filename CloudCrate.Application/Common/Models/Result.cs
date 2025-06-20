using CloudCrate.Application.Common.Errors;

namespace CloudCrate.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; set; }
    public List<Error> Errors { get; set; } = new();

    public static Result Success() => new() { Succeeded = true };

    public static Result Failure(Error error) =>
        new() { Succeeded = false, Errors = new List<Error> { error } };

    public static Result Failure(List<Error> errors) =>
        new() { Succeeded = false, Errors = errors };
}

public class Result<T> : Result
{
    public T? Data { get; set; }

    public static Result<T> Success(T data) =>
        new Result<T> { Succeeded = true, Data = data };

    public new static Result<T> Failure(Error error) =>
        new Result<T> { Succeeded = false, Errors = new List<Error> { error } };

    public new static Result<T> Failure(List<Error> errors) =>
        new Result<T> { Succeeded = false, Errors = errors };
}