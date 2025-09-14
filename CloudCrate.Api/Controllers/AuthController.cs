using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.Auth.Request;
using CloudCrate.Application.DTOs.Auth.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Api.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (result.IsSuccess)
        {
            return Created("", ApiResponse<AuthResponse>.Success(
                data: result.GetValue(),
                message: "User registered successfully",
                statusCode: 201));
        }

        return result.GetError().ToActionResult<AuthResponse>();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<AuthResponse>.Success(
                data: result.GetValue(),
                message: "Login successful"));
        }

        return result.GetError().ToActionResult<AuthResponse>();
    }

    [Authorize]
    [HttpGet("user")]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<UserResponse>.Failure(
                "User is not authenticated", 401));
        }

        var result = await _userService.GetUserByIdAsync(UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<UserResponse>.Success(
                data: result.GetValue(),
                message: "User retrieved successfully"));
        }

        return result.GetError().ToActionResult<UserResponse>();
    }
}