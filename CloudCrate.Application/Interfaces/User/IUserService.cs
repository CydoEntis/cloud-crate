using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.User;

public interface IUserService
{
    Task<Result<UserResponse>> GetUserByIdAsync(string userId);
    Task<Result> CanConsumeStorageAsync(string userId, long bytesToAdd);
    Task<Result> IncrementUsedStorageAsync(string userId, long bytesToAdd);
    Task<Result> DecrementUsedStorageAsync(string userId, long bytesToSubtract);
    Task<List<UserResponse>> GetUsersByIdsAsync(IEnumerable<string> userIds);
    Task<Result> UpdateUserPlanAsync(string userId, SubscriptionPlan newPlan);
}