using Microsoft.AspNetCore.Http;

namespace CloudCrate.Application.Common.Errors;

public static class ErrorStatusMapper
{
    public static int ToStatusCode(Error error) => error switch
    {
        ValidationErrors _ => StatusCodes.Status400BadRequest,
        ValidationError _ => StatusCodes.Status400BadRequest,
        ConflictError _ => StatusCodes.Status409Conflict,
        NotFoundError _ => StatusCodes.Status404NotFound,
        UnauthorizedError _ => StatusCodes.Status401Unauthorized,
        ForbiddenError _ => StatusCodes.Status403Forbidden,
        InternalError _ => StatusCodes.Status500InternalServerError,
        BusinessRuleError _ => StatusCodes.Status400BadRequest,
        FileError _ => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };
}
