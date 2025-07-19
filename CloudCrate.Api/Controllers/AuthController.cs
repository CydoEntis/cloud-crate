using System.Security.Claims;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using Microsoft.AspNetCore.Mvc;
using CloudCrate.Api.Requests.Auth;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request.Email, request.Password);
        return result.ToActionResult(this, 201, "User registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        return result.ToActionResult(this, 200, "Login successful");
    }

    [Authorize]
    [HttpGet("user")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _authService.GetUserByIdAsync(userId);

        return result.ToActionResult(this, 200, "User retrieved successfully");
    }
}