using CloudCrate.Application.DTOs.Auth.Request;
using CloudCrate.Application.DTOs.Auth.Response;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Auth;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<Result<AuthResponse>> LoginAsync(string email, string password);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken);
    Task<Result> LogoutAsync(string userId);
    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request);
}