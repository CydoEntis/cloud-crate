using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Auth.Response;

namespace CloudCrate.Application.Interfaces.Auth;

public interface IAuthService
{
    Task<Result<UserResponse>> RegisterAsync(string email, string password);
    Task<Result<string>> LoginAsync(string email, string password);
    Task<Result<UserResponse>> GetUserByIdAsync(string userId);
}