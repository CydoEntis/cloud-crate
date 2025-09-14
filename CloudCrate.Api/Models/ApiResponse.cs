using CloudCrate.Application.Errors;
using CloudCrate.Application.Models;

namespace CloudCrate.Api.Models;

public class ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public int StatusCode { get; init; }
    public IReadOnlyList<Error>? Errors { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    private ApiResponse(
        bool isSuccess,
        T? data,
        string? message,
        int statusCode,
        IReadOnlyList<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Message = message;
        StatusCode = statusCode;
        Errors = errors;
    }

    public static ApiResponse<T> Success(
        T data,
        string? message = null,
        int statusCode = 200)
    {
        return new ApiResponse<T>(
            isSuccess: true,
            data: data,
            message: message ?? "Operation successful",
            statusCode: statusCode);
    }

    public static ApiResponse<T> Success(
        string? message = null,
        int statusCode = 200)
    {
        return new ApiResponse<T>(
            isSuccess: true,
            data: default,
            message: message ?? "Operation successful",
            statusCode: statusCode);
    }

    public static ApiResponse<T> Failure(
        string message,
        int statusCode = 400,
        IReadOnlyList<Error>? errors = null)
    {
        return new ApiResponse<T>(
            isSuccess: false,
            data: default,
            message: message,
            statusCode: statusCode,
            errors: errors);
    }

    public static ApiResponse<T> FromResult(
        Result<T> result,
        string? successMessage = null,
        int successStatusCode = 200)
    {
        if (result.IsSuccess)
        {
            return Success(
                data: result.GetValue(),
                message: successMessage,
                statusCode: successStatusCode);
        }

        var error = result.GetError();
        var errors = CreateErrorList(error);
        var statusCode = ErrorStatusMapper.ToStatusCode(error);

        return Failure(
            message: error.Message,
            statusCode: statusCode,
            errors: errors);
    }

    public static ApiResponse<T> FromResult(
        Result result,
        string? successMessage = null,
        int successStatusCode = 200)
    {
        if (result.IsSuccess)
        {
            return Success(
                message: successMessage,
                statusCode: successStatusCode);
        }

        var error = result.GetError();
        var errors = CreateErrorList(error);
        var statusCode = ErrorStatusMapper.ToStatusCode(error);

        return Failure(
            message: error.Message,
            statusCode: statusCode,
            errors: errors);
    }

    private static IReadOnlyList<Error> CreateErrorList(Error error)
    {
        return error switch
        {
            ValidationErrors validationErrors => validationErrors.ErrorList.Cast<Error>().ToList(),
            _ => new List<Error> { error }
        };
    }
}

public static class ApiResponseExtensions
{
    public static ApiResponse<T> ToApiResponse<T>(
        this Result<T> result,
        string? successMessage = null,
        int successStatusCode = 200)
    {
        return ApiResponse<T>.FromResult(result, successMessage, successStatusCode);
    }

    public static ApiResponse<object> ToApiResponse(
        this Result result,
        string? successMessage = null,
        int successStatusCode = 200)
    {
        return ApiResponse<object>.FromResult(result, successMessage, successStatusCode);
    }
}