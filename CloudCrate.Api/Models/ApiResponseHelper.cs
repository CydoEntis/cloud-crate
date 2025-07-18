using CloudCrate.Application.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Models;

public static class ApiResponseHelper
{
    public static IActionResult FromErrors<T>(List<Error>? errors)
    {
        if (errors == null || errors.Count == 0)
        {
            var unknownErrorResponse = ApiResponse<T>.Error("An unknown error occurred");
            return new BadRequestObjectResult(unknownErrorResponse);
        }

        var statusCode = errors
            .Select(e => e.StatusCode)
            .OrderBy(code => code)
            .First();

        var message = errors[0].Message;

        var apiResponse = new ApiResponse<T>(false, default, message, statusCode, errors);

        return statusCode switch
        {
            400 => new BadRequestObjectResult(apiResponse),
            401 => new UnauthorizedObjectResult(apiResponse),
            403 => new ObjectResult(apiResponse) { StatusCode = 403 },
            404 => new NotFoundObjectResult(apiResponse),
            409 => new ConflictObjectResult(apiResponse),
            500 => new ObjectResult(apiResponse) { StatusCode = 500 },
            _ => new ObjectResult(apiResponse) { StatusCode = statusCode }
        };
    }
}