using CloudCrate.Api.Models;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Application.DTOs.User.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/user")]
public class UserController : BaseController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<UserResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _userService.GetUserByIdAsync(UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<UserResponse>.Success(
                data: result.GetValue(),
                message: "User profile retrieved successfully"));
        }

        return result.GetError().ToActionResult<UserResponse>();
    }

    [Authorize]
    [HttpPut("display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<object>.Failure("User is not authenticated", 401));
        }

        var result = await _userService.UpdateDisplayNameAsync(UserId, request.DisplayName);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object>.Success(
                data: null,
                message: "Display name updated successfully"));
        }

        return result.GetError().ToActionResult<object>();
    }
}