using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Errors;
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

    public AuthService(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService,
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
                .Select(e => new Error(
                    Code: MapIdentityErrorCodeToErrorCode(e.Code),
                    Message: e.Description))
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
            var errors = new List<Error>
            {
                new Error("ERR_INVALID_CREDENTIALS", "Invalid credentials")
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

    public async Task<Result<UserResponse>> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
            return Result<UserResponse>.Failure(Errors.UserNotFound);

        var crateCount = await _crateService.GetCrateCountAsync(userId);
        var usedStorage = await _crateService.GetTotalUsedStorageAsync(userId);

        var crateLimit = user.Plan switch
        {
            SubscriptionPlan.Free => 3,
            SubscriptionPlan.Pro => 10,
            _ => 3
        };

        var response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            CrateLimit = crateLimit,
            UsedStorage = usedStorage,
            CrateCount = crateCount,
        };

        return Result<UserResponse>.Success(response);
    }


    private string MapIdentityErrorCodeToErrorCode(string code)
    {
        return code switch
        {
            "DuplicateUserName" => "ERR_DUPLICATE_USERNAME",
            "InvalidEmail" => "ERR_INVALID_EMAIL",
            "DuplicateEmail" => "ERR_DUPLICATE_EMAIL",
            "PasswordTooShort" => "ERR_PASSWORD_TOO_SHORT",
            "PasswordRequiresNonAlphanumeric" => "ERR_PASSWORD_REQUIRES_NON_ALPHANUMERIC",
            "PasswordRequiresDigit" => "ERR_PASSWORD_REQUIRES_DIGIT",
            "PasswordRequiresUpper" => "ERR_PASSWORD_REQUIRES_UPPER",
            "PasswordRequiresLower" => "ERR_PASSWORD_REQUIRES_LOWER",
            _ => "ERR_GENERAL"
        };
    }
}