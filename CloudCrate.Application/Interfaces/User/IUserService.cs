using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.User;

public interface IUserService
{
    Task<Result<UserResponse>> GetUserByIdAsync(string userId);

    Task<Result> ReallocateStorageAsync(string userId, int currentCrateAllocationGB,
        int newCrateAllocationGB);
    Task<Result<long>> GetRemainingStorageAsync(string userId);
    Task<Result> IncrementUsedStorageAsync(string userId, long bytesToAdd);
    Task<Result> DecrementUsedStorageAsync(string userId, long bytesToSubtract);
    Task<Result> CanConsumeStorageAsync(string userId, long bytesToAdd);
    Task<List<UserResponse>> GetUsersByIdsAsync(IEnumerable<string> userIds);
    Task<Result> UpdateUserPlanAsync(string userId, SubscriptionPlan newPlan);
    
    Task<Result> AllocateStorageAsync(string userId, long bytesToAllocate);
    Task<Result> DeallocateStorageAsync(string userId, long bytesToDeallocate);
    Task<Result> CanAllocateStorageAsync(string userId, long bytesToAllocate);
    Task<Result> DeleteUserWithCascadeAsync(string targetUserId, string deletingUserId);
}