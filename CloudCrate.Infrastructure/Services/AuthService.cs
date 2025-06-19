using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result> RegisterAsync(string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => MapIdentityErrorCodeToField(e.Code))
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(", ", g.Select(e => e.Description))
                );

            return Result.Failure(errors);
        }

        return Result.Success();
    }

    public async Task<Result<string>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            var errors = new Dictionary<string, string>
            {
                { "email", "Invalid credentials" }
            };

            return Result<string>.Failure(errors);
        }

        var token = _jwtTokenService.GenerateToken(new UserTokenInfo
        {
            UserId = user.Id,
            Email = user.Email!
        });

        return Result<string>.Success(token);
    }

    private string MapIdentityErrorCodeToField(string code)
    {
        return code switch
        {
            "DuplicateUserName" => "email",
            "InvalidEmail" => "email",
            "DuplicateEmail" => "email",
            "PasswordTooShort" => "password",
            "PasswordRequiresNonAlphanumeric" => "password",
            "PasswordRequiresDigit" => "password",
            "PasswordRequiresUpper" => "password",
            "PasswordRequiresLower" => "password",
            _ => "general"
        };
    }
}