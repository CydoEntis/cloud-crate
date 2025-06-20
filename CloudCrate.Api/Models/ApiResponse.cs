using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public int StatusCode { get; set; }
    public List<Error>? Errors { get; set; }

    private ApiResponse(bool success, T? data, string? message, int statusCode, List<Error>? errors = null)
    {
        Success = success;
        Data = data;
        Message = message;
        StatusCode = statusCode;
        Errors = errors;
    }

    // Success factory methods
    public static ApiResponse<T> WithData(T data, string message = "Success", int statusCode = 200) =>
        new ApiResponse<T>(true, data, message, statusCode);

    public static ApiResponse<string> WithMessage(string message, int statusCode = 200) =>
        new ApiResponse<string>(true, null, message, statusCode);

    // Failure factory methods
    public static ApiResponse<T> WithErrors(string message, int statusCode = 400, List<Error>? errors = null) =>
        new ApiResponse<T>(false, default, message, statusCode, errors);

    public static ApiResponse<string> WithMessageErrors(string message, int statusCode = 400) =>
        new ApiResponse<string>(false, null, message, statusCode);

    public static ApiResponse<T> FromFailureResult(Result result, int statusCode = 400) =>
        new ApiResponse<T>(false, default, "Validation failed", statusCode, result.Errors);
}