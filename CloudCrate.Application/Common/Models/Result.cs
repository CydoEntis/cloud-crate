namespace CloudCrate.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; set; }
    public Dictionary<string, string> Errors { get; set; } = new();

    public static Result Success() => new() { Succeeded = true };

    public static Result Failure(string key, string message) =>
        new() { Succeeded = false, Errors = new Dictionary<string, string> { [key] = message } };

    public static Result Failure(Dictionary<string, string> errors) =>
        new() { Succeeded = false, Errors = errors };
}

public class Result<T> : Result
{
    public T? Data { get; set; }

    public static Result<T> Success(T data) =>
        new Result<T> { Succeeded = true, Data = data };

    public new static Result<T> Failure(Dictionary<string, string> errors) =>
        new Result<T> { Succeeded = false, Errors = errors };

    public new static Result<T> Failure(string key, string message) =>
        new Result<T> { Succeeded = false, Errors = new Dictionary<string, string> { [key] = message } };
}