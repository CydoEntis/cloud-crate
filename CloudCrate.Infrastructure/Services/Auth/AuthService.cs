using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Auth.Response;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Services.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<UserResponse>> RegisterAsync(string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .Select(e => IdentityErrorMapper.Map(e.Code, e.Description))
                .ToList();

            return Result<UserResponse>.Failure(errors);
        }

        var userResponse = new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
        };

        return Result<UserResponse>.Success(userResponse);
    }


    public async Task<Result<string>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            return Result<string>.Failure(Errors.User.Unauthorized.WithMessage("Invalid credentials"));
        }

        var accessToken = _jwtTokenService.GenerateToken(new UserTokenInfo
        {
            UserId = user.Id,
            Email = user.Email!
        });

        return Result<string>.Success(accessToken);
    }
}