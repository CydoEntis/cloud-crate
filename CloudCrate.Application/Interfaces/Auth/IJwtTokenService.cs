using System.Security.Claims;
using CloudCrate.Application.DTOs.Auth.Response;

namespace CloudCrate.Application.Interfaces.Auth;

public interface IJwtTokenService
{
    string GenerateAccessToken(UserTokenInfo userInfo);
    string GenerateRefreshToken();
    Task<bool> ValidateRefreshTokenAsync(string userId, string refreshToken);
    Task StoreRefreshTokenAsync(string userId, string refreshToken, DateTime expires);
    Task RevokeRefreshTokenAsync(string userId);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}