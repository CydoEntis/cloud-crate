using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Storage.Response;
using CloudCrate.Application.DTOs.User.Response;

namespace CloudCrate.Application.Interfaces.User;

public interface IUserService
{
    Task<Result<StorageSummaryResponse>> GetUserStorageSummaryAsync(string userId);
    Task<Result<bool>> CanCreateCrateAsync(string userId);
    Task<Result<UserProfileResponse>> GetUserProfileAsync(string userId);
}