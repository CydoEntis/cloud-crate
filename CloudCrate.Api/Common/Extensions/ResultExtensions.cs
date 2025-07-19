using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Common.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller,
        int successStatusCode = 200, string? successMessage = null, string? createdRouteName = null)
    {
        if (result.Succeeded)
        {
            var response = ApiResponse<T>.Success(result.Value!, successMessage ?? "Success", successStatusCode);
            if (successStatusCode == 201 && createdRouteName != null && result.Value != null)
            {
                var id = GetId(result.Value);
                return controller.CreatedAtAction(createdRouteName, new { id }, response);
            }

            if (successStatusCode == 204)
            {
                return controller.NoContent();
            }

            return controller.StatusCode(successStatusCode, response);
        }

        var error = result.Errors.First();

        return error.Code switch
        {
            "ERR_UNAUTHORIZED" => controller.Unauthorized(ApiResponse<T>.Unauthorized(error.Message)),
            "ERR_FORBIDDEN" => controller.StatusCode(403, ApiResponse<T>.Forbidden(error.Message)),
            "ERR_NOT_FOUND" => controller.NotFound(ApiResponse<T>.NotFound(error.Message)),
            "ERR_VALIDATION_FAILED" => controller.BadRequest(ApiResponse<T>.ValidationFailed(result.Errors)),
            _ => controller.StatusCode(error.StatusCode, ApiResponse<T>.Error(error.Message, error.StatusCode)),
        };
    }

    // Overload for non-generic Result (no data)
    public static IActionResult ToActionResult(this Result result, ControllerBase controller,
        int successStatusCode = 200, string? successMessage = null)
    {
        if (result.Succeeded)
        {
            if (successStatusCode == 204)
                return controller.NoContent();

            var response = ApiResponse<object>.Success(null, successMessage ?? "Success", successStatusCode);
            return controller.StatusCode(successStatusCode, response);
        }

        var error = result.Errors.First();

        return error.Code switch
        {
            "ERR_UNAUTHORIZED" => controller.Unauthorized(ApiResponse<object>.Unauthorized(error.Message)),
            "ERR_FORBIDDEN" => controller.StatusCode(403, ApiResponse<object>.Forbidden(error.Message)),
            "ERR_NOT_FOUND" => controller.NotFound(ApiResponse<object>.NotFound(error.Message)),
            "ERR_VALIDATION_FAILED" => controller.BadRequest(ApiResponse<object>.ValidationFailed(result.Errors)),
            _ => controller.StatusCode(error.StatusCode, ApiResponse<object>.Error(error.Message, error.StatusCode)),
        };
    }

    private static object GetId<T>(T value)
    {
        var prop = typeof(T).GetProperty("Id");
        if (prop == null)
            throw new InvalidOperationException("Id property not found on type " + typeof(T).Name);
        return prop.GetValue(value)!;
    }
}
