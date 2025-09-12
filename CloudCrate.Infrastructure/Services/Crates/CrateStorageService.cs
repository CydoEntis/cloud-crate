using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateStorageService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;

    public CrateStorageService(AppDbContext context)
    {
        _context = context;
    }


    public async Task<Result<long>> GetUsedStorageAsync(string userId)
    {
        var usedStorage = await _context.Crates
            .Where(c => c.Members.Any(m => m.UserId == userId && m.Role == CrateRole.Owner))
            .SumAsync(c => c.AllocatedStorageBytes);

        return Result<long>.Success(usedStorage);
    }

    public async Task<Result<long>> GetRemainingStorageAsync(string userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user.IsFailure)
            return Result<long>.Failure(user.Error!);

        var usedResult = await GetUsedStorageAsync(userId);
        if (usedResult.IsFailure) return Result<long>.Failure(usedResult.Error!);

        var remaining = Math.Max(0, user.Value.AllocatedStorageLimitBytes - usedResult.Value);
        return Result<long>.Success(remaining);
    }


    public async Task<Result> ValidateQuotaAsync(string userId, long bytesToAllocate)
    {
        var remainingResult = await GetRemainingStorageAsync(userId);
        if (remainingResult.IsFailure) return Result.Failure(remainingResult.Error!);

        if (bytesToAllocate > remainingResult.Value)
            return Result.Failure(new StorageError("Insufficient storage available"));

        return Result.Success();
    }
}