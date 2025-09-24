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
            var authData = result.GetValue();
            SetRefreshTokenCookie(authData.RefreshToken, authData.RefreshTokenExpires);

            return Created("", ApiResponse<object>.Success(
                data: new
                {
                    accessToken = authData.AccessToken,
                    accessTokenExpires = authData.AccessTokenExpires,
                    tokenType = authData.TokenType,
                },
                message: "User registered successfully",
                statusCode: 201));
        }

        return result.GetError().ToActionResult<object>();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (result.IsSuccess)
        {
            var authData = result.GetValue();
            SetRefreshTokenCookie(authData.RefreshToken, authData.RefreshTokenExpires);

            return Ok(ApiResponse<object>.Success(
                data: new
                {
                    accessToken = authData.AccessToken,
                    accessTokenExpires = authData.AccessTokenExpires,
                    tokenType = authData.TokenType,
                },
                message: "Login successful"));
        }

        return result.GetError().ToActionResult<object>();
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(ApiResponse<object>.Failure(
                "No refresh token provided", 401));
        }

        var result = await _authService.RefreshTokenAsync(refreshToken);

        if (result.IsSuccess)
        {
            var authData = result.GetValue();

            SetRefreshTokenCookie(authData.RefreshToken, authData.RefreshTokenExpires);

            return Ok(ApiResponse<object>.Success(
                data: new
                {
                    accessToken = authData.AccessToken,
                    accessTokenExpires = authData.AccessTokenExpires,
                    tokenType = authData.TokenType
                },
                message: "Token refreshed successfully"));
        }

        ClearRefreshTokenCookie();
        return result.GetError().ToActionResult<object>();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<object>.Failure(
                "User is not authenticated", 401));
        }

        var result = await _authService.LogoutAsync(UserId);

        ClearRefreshTokenCookie();

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object>.Success(
                data: null,
                message: "Logout successful"));
        }

        return result.GetError().ToActionResult<object>();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object>.Success(
                data: null,
                message: "If an account with that email exists, a password reset link has been sent"));
        }

        return result.GetError().ToActionResult<object>();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object>.Success(
                data: null,
                message: "Password reset successfully"));
        }

        return result.GetError().ToActionResult<object>();
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/",
            IsEssential = true
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTime.UtcNow.AddDays(-1)
        };

        Response.Cookies.Append("refreshToken", "", cookieOptions);
    }
}