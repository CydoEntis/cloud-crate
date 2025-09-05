using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Models;
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

    public UserService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<Result<UserResponse>> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result<UserResponse>.Failure(new NotFoundError("User not found"));

        var response = UserMapper.ToUserResponse(user); // maps storage info
        return Result<UserResponse>.Success(response);
    }

    public async Task<Result> IncrementUsedStorageAsync(string userId, long bytesToAdd)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure(new NotFoundError("User not found"));

        try
        {
            user.ConsumeStorage(bytesToAdd); 
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return Result.Failure(new InternalError("Failed to update user storage"));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new StorageError(ex.Message));
        }

        return Result.Success();
    }

    public async Task<Result> DecrementUsedStorageAsync(string userId, long bytesToSubtract)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure(new NotFoundError("User not found"));

        user.ReleaseStorage(bytesToSubtract); // safely decrement storage
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Result.Failure(new InternalError("Failed to update user storage"));

        return Result.Success();
    }

    public async Task<Result> CanConsumeStorageAsync(string userId, long bytesToAdd)
    {
        var userResult = await GetUserByIdAsync(userId);
        if (userResult.IsFailure) return Result.Failure(userResult.Error!);

        if (userResult.Value.UsedStorageBytes + bytesToAdd > userResult.Value.MaxStorageBytes)
            return Result.Failure(new StorageError("Insufficient storage available"));

        return Result.Success();
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
                ProfilePictureUrl = u.ProfilePictureUrl,
                UsedStorageBytes = u.UsedStorageBytes,
                MaxStorageBytes = u.MaxStorageBytes,
                CreatedAt = u.CreatedAt,
                UpdadatedAt = u.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<Result> UpdateUserPlanAsync(string userId, SubscriptionPlan newPlan)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure(new NotFoundError("User not found"));

        try
        {
            user.ChangePlan(newPlan); 
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new StorageError(ex.Message));
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Result.Failure(new InternalError("Failed to update user plan"));

        return Result.Success();
    }

    private async Task<int> GetOwnedCrateCountAsync(string userId)
    {
        return await _context.CrateMembers
            .Where(m => m.UserId == userId && m.Role == CrateRole.Owner)
            .CountAsync();
    }
}
