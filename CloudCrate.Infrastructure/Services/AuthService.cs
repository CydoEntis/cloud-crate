using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Auth;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICrateService _crateService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        ICrateService crateService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _crateService = crateService;
    }

    public async Task<Result> RegisterAsync(string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .Select(e => IdentityErrorMapper.Map(e.Code, e.Description))
                .ToList();

            return Result.Failure(errors);
        }

        return Result.Success();
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

    public async Task<Result<UserResponse>> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
            return Result<UserResponse>.Failure(Errors.User.NotFound);

        var crateCountResult = await _crateService.GetCrateCountAsync(userId);
        if (!crateCountResult.Succeeded)
            return Result<UserResponse>.Failure(crateCountResult.Errors);

        var usedStorageResult = await _crateService.GetTotalUsedStorageAsync(userId);
        if (!usedStorageResult.Succeeded)
            return Result<UserResponse>.Failure(usedStorageResult.Errors);

        var crateLimit = SubscriptionLimits.GetCrateLimit(user.Plan);

        var response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            CrateLimit = crateLimit,
            UsedStorage = usedStorageResult.Value,
            CrateCount = crateCountResult.Value,
            Plan = user.Plan,
        };

        return Result<UserResponse>.Success(response);
    }
}