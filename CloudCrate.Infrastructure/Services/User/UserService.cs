using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Storage.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

    public async Task<UserResponse?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return null;

        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName ?? "Unknown",
            ProfilePictureUrl = user.ProfilePictureUrl
        };
    }

    public async Task<Result<StorageSummaryResponse>> GetUserStorageSummaryAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<StorageSummaryResponse>.Failure(Errors.User.NotFound);

        var totalStorageMb = SubscriptionLimits.GetStorageLimit(user.Plan);

        var ownedCrateIds = await _context.CrateMembers
            .Where(r => r.UserId == userId && r.Role == CrateRole.Owner)
            .Select(r => r.CrateId)
            .ToListAsync();

        var totalBytes = 0L;
        if (ownedCrateIds.Count > 0)
        {
            totalBytes = await _context.FileObjects
                .Where(f => ownedCrateIds.Contains(f.CrateId))
                .SumAsync(f => (long?)f.SizeInBytes) ?? 0;
        }

        var usedStorageMb = Math.Round(totalBytes / 1024.0 / 1024.0, 2);

        var summary = new StorageSummaryResponse
        {
            TotalStorageMb = totalStorageMb,
            UsedStorageMb = usedStorageMb
        };

        return Result<StorageSummaryResponse>.Success(summary);
    }


    public async Task<Result<bool>> CanCreateCrateAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<bool>.Failure(Errors.User.NotFound);

        var crateLimit = SubscriptionLimits.GetCrateLimit(user.Plan);

        var ownedCrateCount = await _context.CrateMembers
            .CountAsync(cm => cm.UserId == userId && cm.Role == CrateRole.Owner);

        return Result<bool>.Success(ownedCrateCount < crateLimit);
    }

    public async Task<Result<UserProfileResponse>> GetUserProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<UserProfileResponse>.Failure(Errors.User.NotFound);

        var storageSummaryResult = await GetUserStorageSummaryAsync(userId);
        if (!storageSummaryResult.Succeeded)
            return Result<UserProfileResponse>.Failure(storageSummaryResult.Errors);

        var canCreateCrateResult = await CanCreateCrateAsync(userId);
        if (!canCreateCrateResult.Succeeded)
            return Result<UserProfileResponse>.Failure(canCreateCrateResult.Errors);

        var crateLimit = SubscriptionLimits.GetCrateLimit(user.Plan);
        var crateCount = await GetOwnedCrateCountAsync(userId);
        var userProfile = new UserProfileResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            ProfilePicture = user.ProfilePictureUrl,
            Plan = user.Plan,
            TotalStorageMb = storageSummaryResult.Value.TotalStorageMb,
            UsedStorageMb = storageSummaryResult.Value.UsedStorageMb,
            CanCreateMoreCrates = canCreateCrateResult.Value,
            CrateLimit = crateLimit,
            CrateCount = crateCount
        };

        return Result<UserProfileResponse>.Success(userProfile);
    }


    public async Task<List<UserResponse>> GetUsersByIdsAsync(IEnumerable<string> userIds)
    {
        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName ?? "Unknown",
                ProfilePictureUrl = u.ProfilePictureUrl
            })
            .ToListAsync();

        return users;
    }

    private async Task<int> GetOwnedCrateCountAsync(string userId)
    {
        return await _context.CrateMembers
            .Where(m => m.UserId == userId && m.Role == CrateRole.Owner)
            .CountAsync();
    }
}