using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Storage.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserMapper = CloudCrate.Infrastructure.Services.User.Mappers.UserMapper;

namespace CloudCrate.Infrastructure.Services.User;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    private const double BytesPerMb = 1024.0 * 1024.0;

    public UserService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<Result<UserResponse>> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<UserResponse>.Failure(new NotFoundError("User not found"));

        var response = UserMapper.ToUserResponse(user);
        return Result<UserResponse>.Success(response);
    }

    public async Task<Result<StorageSummaryResponse>> GetUserStorageSummaryAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<StorageSummaryResponse>.Failure(Error.NotFound("User not found"));

        var totalStorageMb = PlanStorageLimits.GetLimit(user.Plan) / BytesPerMb;

        var ownedCrateIds = await _context.CrateMembers
            .Where(r => r.UserId == userId && r.Role == CrateRole.Owner)
            .Select(r => r.CrateId)
            .ToListAsync();

        long allocatedBytes = 0L;
        long usedBytes = 0L;

        if (ownedCrateIds.Count > 0)
        {
            allocatedBytes = await _context.Crates
                .Where(c => ownedCrateIds.Contains(c.Id))
                .SumAsync(c => (long?)c.AllocatedStorageBytes) ?? 0L;

            usedBytes = await _context.Crates
                .Where(c => ownedCrateIds.Contains(c.Id))
                .SumAsync(c => (long?)c.UsedStorageBytes) ?? 0L;
        }

        var usedStorageMb = Math.Round(usedBytes / BytesPerMb, 2);
        var allocatedStorageMb = Math.Round(allocatedBytes / BytesPerMb, 2);

        var summary = new StorageSummaryResponse
        {
            TotalStorageMb = totalStorageMb,
            UsedStorageMb = usedStorageMb,
            AllocatedStorageMb = allocatedStorageMb
        };

        return Result<StorageSummaryResponse>.Success(summary);
    }

    public async Task<Result<bool>> CanAllocateCrateStorageAsync(string userId, double requestedAllocationGB)
    {
        if (requestedAllocationGB < 0)
            return Result<bool>.Failure(new StorageError("Invalid allocation requested"));

        var summaryResult = await GetUserStorageSummaryAsync(userId);
        if (summaryResult.IsFailure)
            return Result<bool>.Failure(new StorageError("Could not retrieve user storage summary"));

        var summary = summaryResult.Value!;
        var requestedMb = requestedAllocationGB * 1024;

        var canAllocate = requestedMb <= summary.RemainingAllocatableMb;

        return canAllocate
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(new StorageError("Requested allocation exceeds available storage"));
    }

    public async Task<List<UserResponse>> GetUsersByIdsAsync(IEnumerable<string> userIds)
    {
        return await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName ?? "Unknown",
                ProfilePictureUrl = u.ProfilePictureUrl
            })
            .ToListAsync();
    }

    public async Task<Result> UpdateUserPlanAsync(string userId, SubscriptionPlan newPlan)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure(new NotFoundError("User not found"));

        user.ChangePlan(newPlan);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Result.Failure(new InternalError("Failed to update user plan"));
        }

        return Result.Success();
    }

    private async Task<int> GetOwnedCrateCountAsync(string userId)
    {
        return await _context.CrateMembers
            .Where(m => m.UserId == userId && m.Role == CrateRole.Owner)
            .CountAsync();
    }
}