using CloudCrate.Application.DTOs.Auth.Response;

namespace CloudCrate.Application.Interfaces.Auth;

public interface IJwtTokenService
{
    string GenerateToken(UserTokenInfo user);
}