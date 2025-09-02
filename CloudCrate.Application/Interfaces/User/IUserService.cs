using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Storage.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.User;

public interface IUserService
{
    Task<Result<UserResponse>> GetUserByIdAsync(string userId);

    Task<Result<StorageSummaryResponse>> GetUserStorageSummaryAsync(string userId);

    Task<Result<bool>> CanAllocateCrateStorageAsync(string userId, double requestedAllocationMb);


    Task<List<UserResponse>> GetUsersByIdsAsync(IEnumerable<string> userIds);


    Task<Result> UpdateUserPlanAsync(string userId, SubscriptionPlan newPlan);
}