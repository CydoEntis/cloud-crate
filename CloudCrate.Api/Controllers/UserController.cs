using CloudCrate.Api.Models;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.DTOs.User.Response;
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
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _userService.GetUserByIdAsync(UserId!);
        return Response(ApiResponse<UserResponse>.FromResult(result, "User profile retrieved successfully", 200));
    }
}