using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Auth.Request;
using CloudCrate.Application.DTOs.Auth.Response;
using CloudCrate.Application.Interfaces.Auth;
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

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        var user = ApplicationUser.Create(
            email: request.Email,
            displayName: request.DisplayName,
            profilePictureUrl: request.ProfilePictureUrl
        );

        var identityResult = await _userManager.CreateAsync(user, request.Password);

        if (!identityResult.Succeeded)
        {
            var mappedErrors = identityResult.Errors
                .Select(e => IdentityErrorMapper.Map(e.Code, e.Description))
                .ToArray();

            Error errorToReturn = mappedErrors.Length == 1
                ? mappedErrors[0]
                : Error.Validations(mappedErrors.OfType<ValidationError>());

            return Result<AuthResponse>.Failure(errorToReturn);
        }

        var token = _jwtTokenService.GenerateToken(new UserTokenInfo
        {
            UserId = user.Id,
            Email = user.Email!
        });

        return Result<AuthResponse>.Success(new AuthResponse
        {
            Token = token
        });
    }


    public async Task<Result<AuthResponse>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            var error = new UnauthorizedError("Invalid credentials");
            return Result<AuthResponse>.Failure(error);
        }

        var token = _jwtTokenService.GenerateToken(new UserTokenInfo
        {
            UserId = user.Id,
            Email = user.Email!
        });

        return Result<AuthResponse>.Success(new AuthResponse
        {
            Token = token
        });
    }
}