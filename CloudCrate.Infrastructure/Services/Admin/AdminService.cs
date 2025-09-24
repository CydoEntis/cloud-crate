using CloudCrate.Application.DTOs.Admin.Request;
using CloudCrate.Application.DTOs.Admin.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Admin;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Queries;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Admin;

public class AdminService : IAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserService _userService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AdminService> _logger;

    public AdminService(UserManager<ApplicationUser> userManager, IUserService userService,
        IJwtTokenService jwtTokenService, ILogger<AdminService> logger)
    {
        _userManager = userManager;
        _userService = userService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<bool>> IsUserAdminAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            return Result<bool>.Success(user?.IsAdmin == true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking admin status for user {UserId}", userId);
            return Result<bool>.Failure(Error.Internal("Failed to check admin status"));
        }
    }

    public async Task<Result<PaginatedResult<AdminUserResponse>>> GetUsersAsync(AdminUserParameters parameters)
    {
        try
        {
            var query = _userManager.Users.ApplyUserFiltering(parameters);

            var totalCount = await query.CountAsync();

            var users = await query
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync();

            var userResponses = users.Select(u => new AdminUserResponse
            {
                Id = u.Id,
                Email = u.Email!,
                DisplayName = u.DisplayName,
                ProfilePictureUrl = u.ProfilePictureUrl,
                CreatedAt = u.CreatedAt,
                Plan = u.Plan.ToString(),
                UsedStorageBytes = u.UsedStorageBytes,
                IsAdmin = u.IsAdmin,
                IsLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                LockoutEnd = u.LockoutEnd?.DateTime
            }).ToList();

            var paginatedResult = PaginatedResult<AdminUserResponse>.Create(
                userResponses, totalCount, parameters.Page, parameters.PageSize);

            return Result<PaginatedResult<AdminUserResponse>>.Success(paginatedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users list");
            return Result<PaginatedResult<AdminUserResponse>>.Failure(Error.Internal("Failed to retrieve users"));
        }
    }

    // In AdminService.cs
    public async Task<Result> BanUserAsync(string adminUserId, string targetUserId)
    {
        try
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            var targetUser = await _userManager.FindByIdAsync(targetUserId);

            if (adminUser == null)
                return Result.Failure(Error.NotFound("Admin user not found"));

            if (targetUser == null)
                return Result.Failure(Error.NotFound("Target user not found"));

            var adminDomain = adminUser.ToDomain();
            var targetDomain = targetUser.ToDomain();

            if (!adminDomain.CanModifyUser(targetDomain))
            {
                return Result.Failure(Error.Forbidden("Cannot ban this user"));
            }

            var result = await _userManager.SetLockoutEndDateAsync(targetUser, DateTimeOffset.UtcNow.AddYears(100));

            if (result.Succeeded)
            {
                await _jwtTokenService.RevokeRefreshTokenAsync(targetUserId);

                _logger.LogInformation("User {TargetUserId} banned and tokens revoked by admin {AdminUserId}",
                    targetUserId, adminUserId);
                return Result.Success();
            }

            return Result.Failure(Error.Internal("Failed to ban user"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error banning user {TargetUserId} by admin {AdminUserId}", targetUserId, adminUserId);
            return Result.Failure(Error.Internal("Failed to ban user"));
        }
    }

    public async Task<Result> UnbanUserAsync(string adminUserId, string targetUserId)
    {
        try
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            var targetUser = await _userManager.FindByIdAsync(targetUserId);

            if (adminUser == null)
                return Result.Failure(Error.NotFound("Admin user not found"));

            if (targetUser == null)
                return Result.Failure(Error.NotFound("Target user not found"));

            var adminDomain = adminUser.ToDomain();

            if (!adminDomain.CanPerformAdminActions())
            {
                return Result.Failure(Error.Forbidden("Admin privileges required"));
            }

            var result = await _userManager.SetLockoutEndDateAsync(targetUser, null);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {TargetUserId} unbanned by admin {AdminUserId}", targetUserId,
                    adminUserId);
                return Result.Success();
            }

            return Result.Failure(Error.Internal("Failed to unban user"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unbanning user {TargetUserId} by admin {AdminUserId}", targetUserId,
                adminUserId);
            return Result.Failure(Error.Internal("Failed to unban user"));
        }
    }

    public async Task<Result> DeleteUserAsync(string adminUserId, string targetUserId)
    {
        try
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            var targetUser = await _userManager.FindByIdAsync(targetUserId);

            if (adminUser == null)
                return Result.Failure(Error.NotFound("Admin user not found"));

            if (targetUser == null)
                return Result.Failure(Error.NotFound("Target user not found"));

            var adminDomain = adminUser.ToDomain();
            var targetDomain = targetUser.ToDomain();

            if (!adminDomain.CanModifyUser(targetDomain))
            {
                return Result.Failure(Error.Forbidden("Cannot delete this user"));
            }

            var deletionResult = await _userService.DeleteUserWithCascadeAsync(targetUserId, adminUserId);
            if (deletionResult.IsFailure)
            {
                return Result.Failure(deletionResult.GetError());
            }

            _logger.LogWarning("User {TargetUserId} and all associated data deleted by admin {AdminUserId}",
                targetUserId, adminUserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {TargetUserId} by admin {AdminUserId}", targetUserId,
                adminUserId);
            return Result.Failure(Error.Internal("Failed to delete user"));
        }
    }

    public async Task<Result> PromoteToAdminAsync(string adminUserId, string targetUserId)
    {
        try
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            var targetUser = await _userManager.FindByIdAsync(targetUserId);

            if (adminUser == null)
                return Result.Failure(Error.NotFound("Admin user not found"));

            if (targetUser == null)
                return Result.Failure(Error.NotFound("Target user not found"));

            var adminDomain = adminUser.ToDomain();

            if (!adminDomain.CanPerformAdminActions())
            {
                return Result.Failure(Error.Forbidden("Admin privileges required"));
            }

            var targetDomain = targetUser.ToDomain();
            targetDomain.PromoteToAdmin();

            targetUser.IsAdmin = targetDomain.IsAdmin;
            targetUser.UpdatedAt = targetDomain.UpdatedAt;

            var result = await _userManager.UpdateAsync(targetUser);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {TargetUserId} promoted to admin by {AdminUserId}", targetUserId,
                    adminUserId);
                return Result.Success();
            }

            return Result.Failure(Error.Internal("Failed to promote user to admin"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting user {TargetUserId} to admin by {AdminUserId}", targetUserId,
                adminUserId);
            return Result.Failure(Error.Internal("Failed to promote user to admin"));
        }
    }

    public async Task<Result> RemoveAdminAsync(string adminUserId, string targetUserId)
    {
        try
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            var targetUser = await _userManager.FindByIdAsync(targetUserId);

            if (adminUser == null)
                return Result.Failure(Error.NotFound("Admin user not found"));

            if (targetUser == null)
                return Result.Failure(Error.NotFound("Target user not found"));

            var adminDomain = adminUser.ToDomain();
            var targetDomain = targetUser.ToDomain();

            if (!adminDomain.CanModifyUser(targetDomain))
            {
                return Result.Failure(Error.Forbidden("Cannot remove admin privileges from this user"));
            }

            targetDomain.RevokeAdmin();

            targetUser.IsAdmin = targetDomain.IsAdmin;
            targetUser.UpdatedAt = targetDomain.UpdatedAt;

            var result = await _userManager.UpdateAsync(targetUser);

            if (result.Succeeded)
            {
                _logger.LogInformation("Admin privileges removed from user {TargetUserId} by {AdminUserId}",
                    targetUserId, adminUserId);
                return Result.Success();
            }

            return Result.Failure(Error.Internal("Failed to remove admin privileges"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing admin from user {TargetUserId} by {AdminUserId}", targetUserId,
                adminUserId);
            return Result.Failure(Error.Internal("Failed to remove admin privileges"));
        }
    }

    public async Task<Result<AdminStatsResponse>> GetAdminStatsAsync(string adminUserId)
    {
        try
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            if (adminUser?.IsAdmin != true)
            {
                return Result<AdminStatsResponse>.Failure(Error.Forbidden("Admin privileges required"));
            }

            var users = await _userManager.Users.ToListAsync();

            var stats = new AdminStatsResponse
            {
                TotalUsers = users.Count,
                AdminUsers = users.Count(u => u.IsAdmin),
                LockedUsers = users.Count(u => u.LockoutEnd > DateTimeOffset.UtcNow),
                ActiveUsers = users.Count(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow),
                TotalStorageUsed = users.Sum(u => u.UsedStorageBytes),
                LastUserRegistered = users.MaxBy(u => u.CreatedAt)?.CreatedAt,
                UsersByPlan = users.GroupBy(u => u.Plan.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return Result<AdminStatsResponse>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin stats for user {AdminUserId}", adminUserId);
            return Result<AdminStatsResponse>.Failure(Error.Internal("Failed to retrieve admin statistics"));
        }
    }

    public async Task<Result> UpdateUserPlanAsync(string adminUserId, string targetUserId, SubscriptionPlan newPlan)
    {
        try
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            if (adminUser?.IsAdmin != true)
                return Result.Failure(Error.Forbidden("Admin privileges required"));

            var result = await _userService.UpdateUserPlanAsync(targetUserId, newPlan);
            if (result.IsSuccess)
            {
                _logger.LogInformation("User {TargetUserId} plan updated to {NewPlan} by admin {AdminUserId}",
                    targetUserId, newPlan, adminUserId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plan for user {TargetUserId} by admin {AdminUserId}",
                targetUserId, adminUserId);
            return Result.Failure(Error.Internal("Failed to update user plan"));
        }
    }
}