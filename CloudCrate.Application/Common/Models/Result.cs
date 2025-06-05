namespace CloudCrate.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; set; }
    public List<string> Errors { get; set; } = new();

    public static Result Success() => new() { Succeeded = true };

    public static Result Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors.ToList() };
}

public class Result<T> : Result
{
    public T? Data { get; set; }

    public static Result<T> Success(T data) =>
        new Result<T> { Succeeded = true, Data = data };

    public new static Result<T> Failure(params string[] errors) =>
        new Result<T> { Succeeded = false, Errors = errors.ToList() };
}