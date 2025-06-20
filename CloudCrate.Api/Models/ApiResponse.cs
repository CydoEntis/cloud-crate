using CloudCrate.Application.Common.Errors; // contains Error class

namespace CloudCrate.Api.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }

    public List<Error>? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T data) => new()
    {
        Success = true,
        Data = data,
        Errors = null
    };

    public static ApiResponse<T> FailResponse(List<Error> errors) => new()
    {
        Success = false,
        Data = default,
        Errors = errors
    };

    public static ApiResponse<T> FailResponse(Error error) =>
        FailResponse(new List<Error> { error });
}