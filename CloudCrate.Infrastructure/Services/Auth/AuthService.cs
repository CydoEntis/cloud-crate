using CloudCrate.Application.DTOs.Auth.Request;
using CloudCrate.Application.DTOs.Auth.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using CloudCrate.Application.Interfaces.Invite;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IUserInviteService _userInviteService;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        IUserInviteService userInviteService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _userInviteService = userInviteService;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InviteToken))
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized("Registration requires a valid invite"));
        }

        var inviteResult = await _userInviteService.ValidateInviteTokenAsync(request.InviteToken);
        if (!inviteResult.IsSuccess)
        {
            return Result<AuthResponse>.Failure(inviteResult.GetError());
        }

        var invite = inviteResult.GetValue();

        if (!string.IsNullOrWhiteSpace(invite.Email) &&
            !invite.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized("This invite is for a different email address"));
        }

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

        var markUsedResult = await _userInviteService.MarkInviteAsUsedAsync(request.InviteToken, userEntity.Id);

        var accessToken = _jwtTokenService.GenerateAccessToken(new UserTokenInfo
        {
            UserId = userEntity.Id,
            Email = userEntity.Email!
        });

        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenExpires = DateTime.UtcNow.AddDays(7);

        await _jwtTokenService.StoreRefreshTokenAsync(userEntity.Id, refreshToken, refreshTokenExpires);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpires = refreshTokenExpires,
            TokenType = "Bearer",
            User = new AuthResponse.AuthUser
            {
                Id = userEntity.Id,
                Email = userEntity.Email!,
                DisplayName = userEntity.DisplayName,
                IsAdmin = userEntity.IsAdmin 
            }
        });
    }

    public async Task<Result<AuthResponse>> LoginAsync(string email, string password)
    {
        var userEntity = await _userManager.FindByEmailAsync(email);
        if (userEntity == null || !await _userManager.CheckPasswordAsync(userEntity, password))
        {
            return Result<AuthResponse>.Failure(new UnauthorizedError("Invalid credentials"));
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(new UserTokenInfo
        {
            UserId = userEntity.Id,
            Email = userEntity.Email!
        });

        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenExpires = DateTime.UtcNow.AddDays(7);

        await _jwtTokenService.StoreRefreshTokenAsync(userEntity.Id, refreshToken, refreshTokenExpires);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpires = refreshTokenExpires,
            TokenType = "Bearer",
            User = new AuthResponse.AuthUser
            {
                Id = userEntity.Id,
                Email = userEntity.Email!,
                DisplayName = userEntity.DisplayName,
                IsAdmin = userEntity.IsAdmin 
            }
        });
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<AuthResponse>.Failure(new UnauthorizedError("Invalid refresh token"));
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

        if (user == null)
        {
            return Result<AuthResponse>.Failure(new UnauthorizedError("Invalid refresh token"));
        }

        var isValidRefreshToken = await _jwtTokenService.ValidateRefreshTokenAsync(user.Id, request.RefreshToken);
        if (!isValidRefreshToken)
        {
            return Result<AuthResponse>.Failure(new UnauthorizedError("Refresh token expired or invalid"));
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(new UserTokenInfo
        {
            UserId = user.Id,
            Email = user.Email!
        });

        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var newRefreshTokenExpires = DateTime.UtcNow.AddDays(7);

        await _jwtTokenService.StoreRefreshTokenAsync(user.Id, newRefreshToken, newRefreshTokenExpires);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpires = newRefreshTokenExpires,
            TokenType = "Bearer"
        });
    }

    public async Task<Result> LogoutAsync(string userId)
    {
        await _jwtTokenService.RevokeRefreshTokenAsync(userId);
        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Result.Success();
        }

        var resetToken = GeneratePasswordResetToken();
        var resetTokenExpires = DateTime.UtcNow.AddHours(1);

        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpires = resetTokenExpires;
        await _userManager.UpdateAsync(user);

        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "https://localhost:5173";
        var resetLink = $"{frontendUrl}/reset-password?token={resetToken}&email={Uri.EscapeDataString(request.Email)}";

        var emailModel = new
        {
            DisplayName = user.DisplayName,
            ResetLink = resetLink,
            ExpiresAt = resetTokenExpires
        };

        var emailResult = await _emailService.SendEmailAsync(
            request.Email,
            "Reset Your CloudCrate Password",
            "PasswordReset",
            emailModel
        );

        if (!emailResult.IsSuccess)
        {
            return Result.Failure(new EmailSendError("Failed to send password reset email"));
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Result.Failure(new NotFoundError("User not found"));
        }

        if (string.IsNullOrWhiteSpace(user.PasswordResetToken) ||
            user.PasswordResetToken != request.Token ||
            user.PasswordResetTokenExpires == null ||
            user.PasswordResetTokenExpires <= DateTime.UtcNow)
        {
            return Result.Failure(new UnauthorizedError("Invalid or expired reset token"));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            var mappedErrors = result.Errors
                .Select(e => IdentityErrorMapper.Map(e.Code, e.Description))
                .ToArray();

            Error errorToReturn = mappedErrors.Length == 1
                ? mappedErrors[0]
                : Error.Validations(mappedErrors.OfType<ValidationError>());

            return Result.Failure(errorToReturn);
        }

        user.PasswordResetToken = null;
        user.PasswordResetTokenExpires = null;
        await _userManager.UpdateAsync(user);
        await _jwtTokenService.RevokeRefreshTokenAsync(user.Id);

        return Result.Success();
    }

    private string GeneratePasswordResetToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}