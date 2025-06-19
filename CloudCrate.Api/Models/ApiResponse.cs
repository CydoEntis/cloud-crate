namespace CloudCrate.Api.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }

    // Unified error dictionary: "email": "Invalid", "general": "Something went wrong"
    public Dictionary<string, string>? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T data) => new()
    {
        Success = true,
        Data = data,
        Errors = null
    };

    public static ApiResponse<T> FailResponse(Dictionary<string, string> errors) => new()
    {
        Success = false,
        Data = default,
        Errors = errors
    };

    public static ApiResponse<T> FailResponse(string key, string message) =>
        FailResponse(new Dictionary<string, string> { [key] = message });
}