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
            return Result.Failure(result.Errors.Select(e => e.Description).ToArray());

        return Result.Success();
    }

    public async Task<Result<string>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            return Result<string>.Failure("Invalid credentials");
        }

        var token = _jwtTokenService.GenerateToken(new UserTokenInfo
        {
            UserId = user.Id,
            Email = user.Email!
        });

        return Result<string>.Success(token);
    }
}