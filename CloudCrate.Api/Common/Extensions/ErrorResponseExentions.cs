using CloudCrate.Api.Models;
using CloudCrate.Application.Errors;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Common.Extensions;

public static class ErrorResponseExtensions
{
    public static IActionResult ToActionResult<T>(this Error error)
    {
        var statusCode = ErrorStatusMapper.ToStatusCode(error);
        var errors = CreateErrorList(error);

        var response = ApiResponse<T>.Failure(error.Message, statusCode, errors);

        return statusCode switch
        {
            400 => new BadRequestObjectResult(response),
            401 => new UnauthorizedObjectResult(response),
            403 => new ObjectResult(response) { StatusCode = 403 },
            404 => new NotFoundObjectResult(response),
            409 => new ConflictObjectResult(response),
            500 => new ObjectResult(response) { StatusCode = 500 },
            _ => new ObjectResult(response) { StatusCode = statusCode }
        };
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