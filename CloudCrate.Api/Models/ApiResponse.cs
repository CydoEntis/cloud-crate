using CloudCrate.Application.Common.Errors;

namespace CloudCrate.Api.Models;

public class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public int StatusCode { get; set; }
    public List<Error>? Errors { get; set; }

    private ApiResponse(bool isSuccess, T? data, string? message, int statusCode, List<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Message = message;
        StatusCode = statusCode;
        Errors = errors;
    }

    public static ApiResponse<T> Success(T data, string message = "Success", int statusCode = 200) =>
        new ApiResponse<T>(true, data, message, statusCode);

    public static ApiResponse<T> SuccessMessage(string message = "Success", int statusCode = 200) =>
        new ApiResponse<T>(true, default, message, statusCode);

    public static ApiResponse<T> ValidationFailed(List<Error> errors, string message = "Validation failed") =>
        new ApiResponse<T>(false, default, message, 400, errors);

    public static ApiResponse<T> Unauthorized(string message = "Unauthorized") =>
        new ApiResponse<T>(false, default, message, 401);

    public static ApiResponse<T> Forbidden(string message = "Forbidden") =>
        new ApiResponse<T>(false, default, message, 403);

    public static ApiResponse<T> NotFound(string message = "Not Found") =>
        new ApiResponse<T>(false, default, message, 404);

    public static ApiResponse<T> Error(string message = "An error occurred", int statusCode = 400) =>
        new ApiResponse<T>(false, default, message, statusCode);
}