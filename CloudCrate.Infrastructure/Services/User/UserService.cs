using CloudCrate.Application.DTOs.User.Mappers;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.User;

public sealed record StorageError(string Message = "Storage operation failed") : Error(Message);

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<UserService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Result<UserResponse>> GetUserByIdAsync(string userId)
    {
        try
        {
            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result<UserResponse>.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();
            var response = MapToUserResponse(domainUser);

            return Result<UserResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetUserByIdAsync for UserId {UserId}", userId);
            return Result<UserResponse>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> ReallocateStorageAsync(string userId, int currentCrateAllocationGB,
        int newCrateAllocationGB)
    {
        var allocationDifferenceGb = newCrateAllocationGB - currentCrateAllocationGB;

        if (allocationDifferenceGb == 0)
            return Result.Success();

        if (allocationDifferenceGb > 0)
        {
            var canAllocate = await CanAllocateStorageAsync(userId, allocationDifferenceGb);
            if (!canAllocate.IsSuccess)
                return Result.Failure(canAllocate.GetError());

            return await AllocateStorageAsync(userId, StorageSize.FromGigabytes(allocationDifferenceGb).Bytes);
        }

        return await DeallocateStorageAsync(userId, StorageSize.FromGigabytes(Math.Abs(allocationDifferenceGb)).Bytes);
    }

    public async Task<Result<long>> GetRemainingStorageAsync(string userId)
    {
        try
        {
            var userEntity = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (userEntity == null)
                return Result<long>.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();
            return Result<long>.Success(domainUser.RemainingUsageBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetRemainingStorageAsync for UserId {UserId}", userId);
            return Result<long>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> IncrementUsedStorageAsync(string userId, long bytesToAdd)
    {
        try
        {
            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();

            try
            {
                domainUser.ConsumeStorage(bytesToAdd);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }

            userEntity.UsedStorageBytes = domainUser.UsedStorageBytes;
            userEntity.UpdatedAt = domainUser.UpdatedAt;

            var result = await _userManager.UpdateAsync(userEntity);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return Result.Failure(new InternalError($"Failed to update user storage: {errors}"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in IncrementUsedStorageAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> DecrementUsedStorageAsync(string userId, long bytesToSubtract)
    {
        try
        {
            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();

            try
            {
                domainUser.ReleaseStorage(bytesToSubtract);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }

            userEntity.UsedStorageBytes = domainUser.UsedStorageBytes;
            userEntity.UpdatedAt = domainUser.UpdatedAt;

            var result = await _userManager.UpdateAsync(userEntity);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return Result.Failure(new InternalError($"Failed to update user storage: {errors}"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DecrementUsedStorageAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> AllocateStorageAsync(string userId, long bytesToAllocate)
    {
        try
        {
            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();

            try
            {
                domainUser.AllocateStorage(bytesToAllocate);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }

            userEntity.AllocatedStorageBytes = domainUser.AllocatedStorageBytes;
            userEntity.UpdatedAt = domainUser.UpdatedAt;

            var result = await _userManager.UpdateAsync(userEntity);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return Result.Failure(new InternalError($"Failed to allocate storage: {errors}"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in AllocateStorageAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> DeallocateStorageAsync(string userId, long bytesToDeallocate)
    {
        try
        {
            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();

            try
            {
                domainUser.DeallocateStorage(bytesToDeallocate);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }

            userEntity.AllocatedStorageBytes = domainUser.AllocatedStorageBytes;
            userEntity.UpdatedAt = domainUser.UpdatedAt;

            var result = await _userManager.UpdateAsync(userEntity);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return Result.Failure(new InternalError($"Failed to deallocate storage: {errors}"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeallocateStorageAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> CanAllocateStorageAsync(string userId, long allocatedGb)
    {
        try
        {
            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();

            var requestedBytes = StorageSize.FromGigabytes(allocatedGb).Bytes;

            if (domainUser.RemainingAllocationBytes < requestedBytes)
            {
                _logger.LogWarning("User {UserId} cannot allocate {AllocatedGb}GB: insufficient quota",
                    userId, allocatedGb);
                return Result.Failure(new StorageError("Insufficient storage quota available for allocation"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CanAllocateStorageAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> CanConsumeStorageAsync(string userId, long bytesToAdd)
    {
        try
        {
            var userResult = await GetUserByIdAsync(userId);
            if (userResult.IsFailure)
                return Result.Failure(userResult.GetError());

            var user = userResult.GetValue();
            if (user.UsedStorageBytes + bytesToAdd > user.AccountStorageLimitBytes)
                return Result.Failure(new StorageError("Insufficient storage available"));

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CanConsumeStorageAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<List<UserResponse>> GetUsersByIdsAsync(IEnumerable<string> userIds)
    {
        try
        {
            var userEntities = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            var responses = new List<UserResponse>();
            foreach (var userEntity in userEntities)
            {
                var domainUser = userEntity.ToDomain();
                var response = MapToUserResponse(domainUser);
                responses.Add(response);
            }

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetUsersByIdsAsync");
            return new List<UserResponse>();
        }
    }

    public async Task<Result> UpdateUserPlanAsync(string userId, SubscriptionPlan newPlan)
    {
        try
        {
            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();

            try
            {
                domainUser.ChangePlan(newPlan);
            }
            catch (ArgumentException ex)
            {
                return Result.Failure(new ValidationError(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(new ValidationError(ex.Message));
            }

            userEntity.Plan = domainUser.Plan;
            userEntity.UpdatedAt = domainUser.UpdatedAt;

            var result = await _userManager.UpdateAsync(userEntity);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return Result.Failure(new InternalError($"Failed to update user plan: {errors}"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateUserPlanAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError("An unexpected error occurred while updating the user plan"));
        }
    }

    public async Task<Result> UpdateDisplayNameAsync(string userId, string displayName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return Result.Failure(new ValidationError("Display name cannot be empty", "DisplayName"));

            if (displayName.Length < 2)
                return Result.Failure(new ValidationError("Display name must be at least 2 characters", "DisplayName"));

            if (displayName.Length > 50)
                return Result.Failure(
                    new ValidationError("Display name must be less than 50 characters", "DisplayName"));

            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity == null)
                return Result.Failure(new NotFoundError("User not found"));

            userEntity.DisplayName = displayName;
            userEntity.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(userEntity);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return Result.Failure(new InternalError($"Failed to update display name: {errors}"));
            }

            _logger.LogInformation("Display name updated for user {UserId}", userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateDisplayNameAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }
    
    public async Task<Result<UserResponse>> GetUserByEmailAsync(string email)
    {
        try
        {
            var userEntity = await _userManager.FindByEmailAsync(email);
            if (userEntity == null)
                return Result<UserResponse>.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();
            var response = MapToUserResponse(domainUser);

            return Result<UserResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetUserByEmailAsync for Email {Email}", email);
            return Result<UserResponse>.Failure(new InternalError(ex.Message));
        }
    }


    private async Task UpdateUserReferencesAsync(string targetUserId, string replacementUserId)
    {
        await _context.CrateFiles
            .Where(f => f.DeletedByUserId == targetUserId)
            .ExecuteUpdateAsync(f => f.SetProperty(x => x.DeletedByUserId, replacementUserId));

        await _context.CrateFiles
            .Where(f => f.RestoredByUserId == targetUserId)
            .ExecuteUpdateAsync(f => f.SetProperty(x => x.RestoredByUserId, replacementUserId));

        await _context.CrateFolders
            .Where(f => f.DeletedByUserId == targetUserId)
            .ExecuteUpdateAsync(f => f.SetProperty(x => x.DeletedByUserId, replacementUserId));

        await _context.CrateFolders
            .Where(f => f.RestoredByUserId == targetUserId)
            .ExecuteUpdateAsync(f => f.SetProperty(x => x.RestoredByUserId, replacementUserId));
    }

    private async Task<int> GetOwnedCrateCountAsync(string userId)
    {
        try
        {
            return await _context.CrateMembers
                .Where(m => m.UserId == userId && m.Role == CrateRole.Owner)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetOwnedCrateCountAsync for UserId {UserId}", userId);
            return 0;
        }
    }

    private static UserResponse MapToUserResponse(UserAccount domainUser)
    {
        return new UserResponse
        {
            Id = domainUser.Id,
            Email = domainUser.Email,
            DisplayName = domainUser.DisplayName,
            ProfilePictureUrl = domainUser.ProfilePictureUrl,
            UsedStorageBytes = domainUser.UsedStorageBytes,
            AllocatedStorageBytes = domainUser.AllocatedStorageBytes,
            AccountStorageLimitBytes = domainUser.AccountStorageLimitBytes,
            RemainingAllocationBytes = domainUser.RemainingAllocationBytes,
            RemainingUsageBytes = domainUser.RemainingUsageBytes,
            CreatedAt = domainUser.CreatedAt,
            UpdatedAt = domainUser.UpdatedAt,
            IsAdmin = domainUser.IsAdmin,
            IsDemoAccount = domainUser.IsDemoAccount,
        };
    }
}