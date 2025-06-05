using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.Auth;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
            return BadRequest(result.Errors);

        return Ok();
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (!result.Succeeded)
            return Unauthorized(result.Errors);

        return Ok(new { token = result.Data });
    }
}