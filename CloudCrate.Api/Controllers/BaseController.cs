using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Errors;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected new IActionResult Response<T>(ApiResponse<T> response) =>
        StatusCode(response.StatusCode, response);
    
    protected new IActionResult Response(ApiResponse response) =>
        StatusCode(response.StatusCode, response);

    protected string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected IActionResult? EnsureUserAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            var response = new ApiResponse<string>(
                isSuccess: false,
                value: null,
                message: "User is not authenticated",
                statusCode: 401
            );
            return Response(response);
        }

        return null;
    }

    protected IActionResult? EnsureRouteIdMatches(Guid routeId, Guid bodyId, string name)
    {
        if (routeId != bodyId)
        {
            var response = new ApiResponse<string>(
                isSuccess: false,
                value: null,
                message: $"{name} ID in route and request body do not match",
                statusCode: 400
            );
            return Response(response);
        }

        return null;
    }
}