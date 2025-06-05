using CloudCrate.Api.Models;

namespace CloudCrate.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(UserTokenInfo user);
}