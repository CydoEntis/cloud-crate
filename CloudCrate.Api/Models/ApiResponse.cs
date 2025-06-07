namespace CloudCrate.Api.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }  
    public T? Data { get; set; }
    public IEnumerable<string>? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T data) => new()
    {
        Success = true,
        Data = data,
        Errors = null
    };

    public static ApiResponse<T> FailResponse(IEnumerable<string> errors) => new()
    {
        Success = false,
        Data = default,
        Errors = errors
    };
    
    public static ApiResponse<T> FailResponse(string error) => FailResponse([error]);
}