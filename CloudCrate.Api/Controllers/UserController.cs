using System.Security.Claims;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Application.Interfaces.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/user")]
public class UserController : ControllerBase
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _userService.GetUserProfileAsync(userId);

        return result.ToActionResult(this, 200, "User profile retrieved successfully");
    }
}