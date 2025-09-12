using CloudCrate.Application.DTOs.Auth.Request;
using CloudCrate.Application.DTOs.Auth.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence.Mappers;
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
        var domainUser = UserAccount.Create(
            id: Guid.NewGuid().ToString(),
            email: request.Email,
            displayName: request.DisplayName,
            profilePictureUrl: request.ProfilePictureUrl
        );

        var userEntity = domainUser.ToEntity();

        var identityResult = await _userManager.CreateAsync(userEntity, request.Password);
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
            UserId = userEntity.Id,
            Email = userEntity.Email!
        });

        return Result<AuthResponse>.Success(new AuthResponse
        {
            Token = token
        });
    }

    public async Task<Result<AuthResponse>> LoginAsync(string email, string password)
    {
        var userEntity = await _userManager.FindByEmailAsync(email);
        if (userEntity == null || !await _userManager.CheckPasswordAsync(userEntity, password))
        {
            return Result<AuthResponse>.Failure(new UnauthorizedError("Invalid credentials"));
        }

        var domainUser = userEntity.ToDomain();

        var token = _jwtTokenService.GenerateToken(new UserTokenInfo
        {
            UserId = domainUser.Id,
            Email = domainUser.Email
        });

        return Result<AuthResponse>.Success(new AuthResponse
        {
            Token = token
        });
    }
}
