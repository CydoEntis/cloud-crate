using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
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

            // Get allocated storage from owned crates
            var allocatedSoFar = await _context.CrateMembers
                .Where(cm => cm.UserId == userId && cm.Role == CrateRole.Owner)
                .Include(cm => cm.Crate)
                .SumAsync(cm => cm.Crate.AllocatedStorageBytes);

            // Convert to domain entity first, then to response
            var domainUser = userEntity.ToDomain();
            var response = MapToUserResponse(domainUser, allocatedSoFar);

            return Result<UserResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetUserByIdAsync for UserId {UserId}", userId);
            return Result<UserResponse>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<long>> GetRemainingStorageAsync(string userId)
    {
        try
        {
            var userEntity = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (userEntity == null)
                return Result<long>.Failure(new NotFoundError("User not found"));

            var domainUser = userEntity.ToDomain();
            return Result<long>.Success(domainUser.RemainingStorageBytes);
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

            // Convert to domain entity, apply business logic
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

            // Update the Identity entity with domain changes
            userEntity.UsedAccountStorageBytes = domainUser.UsedStorageBytes;
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

            // Convert to domain entity, apply business logic
            var domainUser = userEntity.ToDomain();

            try
            {
                domainUser.ReleaseStorage(bytesToSubtract);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }

            // Update the Identity entity with domain changes
            userEntity.UsedAccountStorageBytes = domainUser.UsedStorageBytes;
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

    public async Task<Result> CanConsumeStorageAsync(string userId, long bytesToAdd)
    {
        try
        {
            var userResult = await GetUserByIdAsync(userId);
            if (userResult.IsFailure)
                return Result.Failure(userResult.Error!);

            var user = userResult.Value!;
            if (user.UsedAccountStorageBytes + bytesToAdd > user.AllocatedStorageLimitBytes)
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
                // Get allocated storage for this user
                var allocatedSoFar = await _context.CrateMembers
                    .Where(cm => cm.UserId == userEntity.Id && cm.Role == CrateRole.Owner)
                    .Include(cm => cm.Crate)
                    .SumAsync(cm => cm.Crate.AllocatedStorageBytes);

                var domainUser = userEntity.ToDomain();
                var response = MapToUserResponse(domainUser, allocatedSoFar);
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

            // Convert to domain entity, apply business logic
            var domainUser = userEntity.ToDomain();

            try
            {
                domainUser.ChangePlan(newPlan);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(new StorageError(ex.Message));
            }

            // Update the Identity entity with domain changes
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
            return Result.Failure(new InternalError(ex.Message));
        }
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

    private static UserResponse MapToUserResponse(UserAccount domainUser, long allocatedStorageSoFar)
    {
        return new UserResponse
        {
            Id = domainUser.Id,
            Email = domainUser.Email,
            DisplayName = domainUser.DisplayName,
            ProfilePictureUrl = domainUser.ProfilePictureUrl,
            UsedAccountStorageBytes = domainUser.UsedStorageBytes,
            AllocatedStorageLimitBytes = domainUser.AllocatedStorageLimitBytes,
            RemainingAllocatableBytes = domainUser.AllocatedStorageLimitBytes - allocatedStorageSoFar,
            CreatedAt = domainUser.CreatedAt,
            UpdatedAt = domainUser.UpdatedAt
        };
    }
}