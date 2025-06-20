using System.Security.Claims;
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

        if (!result.Succeeded)
        {
            var errorResponse = ApiResponse<object>.FromFailureResult(result, 400);
            return BadRequest(errorResponse);
        }

        var successResponse = ApiResponse<object>.WithMessage("Registration successful", 200);
        return Ok(successResponse);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (!result.Succeeded)
        {
            var errorResponse = ApiResponse<object>.FromFailureResult(result, 400);
            return BadRequest(errorResponse);
        }

        var successResponse = ApiResponse<object>.WithData(new { accessToken = result.Data }, "Login successful", 200);
        return Ok(successResponse);
    }

    [HttpGet("user")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.WithMessage("User ID not found in token", 401));

        var result = await _authService.GetUserByIdAsync(userId);

        if (!result.Succeeded)
            return NotFound(ApiResponse<string>.WithErrors(result.Errors[0].Message, 404, result.Errors));

        return Ok(ApiResponse<object>.WithData(result.Data!, "User retrieved successfully"));
    }
}