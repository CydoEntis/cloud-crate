using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Common.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.Succeeded)
            return new OkObjectResult(ApiResponse<T>.Success(result.Value!));

        return ApiResponseHelper.FromErrors<T>(result.Errors);
    }

    public static IActionResult ToActionResult(this Result result)
    {
        if (result.Succeeded)
            return new OkObjectResult(ApiResponse<string>.SuccessMessage());

        return ApiResponseHelper.FromErrors<string>(result.Errors);
    }
}