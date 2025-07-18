using CloudCrate.Application.Common.Errors;
using System.Collections.Generic;

namespace CloudCrate.Api.Models
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; init; }
        public T? Value { get; init; }
        public string? Message { get; init; }
        public int StatusCode { get; init; }
        public List<Error>? Errors { get; init; }

        public ApiResponse(bool isSuccess, T? value, string? message, int statusCode, List<Error>? errors = null)
        {
            IsSuccess = isSuccess;
            Value = value;
            Message = message;
            StatusCode = statusCode;
            Errors = errors;
        }

        public static ApiResponse<T> Success(T data, string message = "Success", int statusCode = 200) =>
            new(true, data, message, statusCode);

        public static ApiResponse<T> SuccessMessage(string message = "Success", int statusCode = 200) =>
            new(true, default, message, statusCode);

        public static ApiResponse<T> ValidationFailed(List<Error> errors, string message = "Validation failed") =>
            new(false, default, message, 400, errors);

        public static ApiResponse<T> Unauthorized(string message = "Unauthorized") =>
            new(false, default, message, 401);

        public static ApiResponse<T> Forbidden(string message = "Forbidden") =>
            new(false, default, message, 403);

        public static ApiResponse<T> NotFound(string message = "Not Found") =>
            new(false, default, message, 404);

        public static ApiResponse<T> Error(string message = "An error occurred", int statusCode = 400) =>
            new(false, default, message, statusCode);
    }
}