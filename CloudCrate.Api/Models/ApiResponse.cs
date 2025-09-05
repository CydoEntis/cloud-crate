using CloudCrate.Application.Errors;
using CloudCrate.Application.Models;

namespace CloudCrate.Api.Models;

public class ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Message { get; init; }
    public int StatusCode { get; init; }
    public IReadOnlyList<Error>? Errors { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public ApiResponse(
        bool isSuccess,
        T? value,
        string? message,
        int statusCode,
        IReadOnlyList<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Message = message;
        StatusCode = statusCode;
        Errors = errors;
    }

    public static ApiResponse<T> Success(
        T? data = default,
        string message = "Success",
        int statusCode = 200)
    {
        return new ApiResponse<T>(true, data, message, statusCode);
    }

    /// Converts a Result<T> from the Application layer into a standardized ApiResponse.
    public static ApiResponse<T> FromResult(
        Result<T> result,
        string? message = null,
        int successStatusCode = 200)
    {
        if (result.IsSuccess)
            return Success(result.Value, message ?? "Operation succeeded", successStatusCode);

        IReadOnlyList<Error>? errors = result.Error switch
        {
            ValidationErrors ve => ve.ErrorList.Cast<Error>().ToList(),
            _ => new List<Error> { result.Error! }
        };

        int statusCode = ErrorStatusMapper.ToStatusCode(result.Error!);

        return new ApiResponse<T>(
            isSuccess: false,
            value: default,
            message: message ?? result.Error?.Message,
            statusCode: statusCode,
            errors: errors
        );
    }
}


public class ApiResponse
{
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public int StatusCode { get; init; }
    public IReadOnlyList<Error>? Errors { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    private ApiResponse(bool isSuccess, string? message, int statusCode, IReadOnlyList<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Message = message;
        StatusCode = statusCode;
        Errors = errors;
    }

    private static ApiResponse Success(string? message = "Success", int statusCode = 200)
        => new ApiResponse(true, message, statusCode);

    private static ApiResponse Error(string? message, int statusCode = 400, IReadOnlyList<Error>? errors = null)
        => new ApiResponse(false, message, statusCode, errors);

    public static ApiResponse FromResult(Result result, string? message = null, int successStatusCode = 200)
    {
        if (result.IsSuccess)
            return Success(message ?? "Operation succeeded", successStatusCode);

        var errors = result.Error != null ? new List<Error> { result.Error } : null;
        int statusCode = result.Error != null ? ErrorStatusMapper.ToStatusCode(result.Error) : 400;

        return Error(message ?? result.Error?.Message, statusCode, errors);
    }
}
