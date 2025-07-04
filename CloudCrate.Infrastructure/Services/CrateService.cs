using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services;

public class CrateService : ICrateService
{
    private readonly IAppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CrateService(IAppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<bool> CanCreateCrateAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        var crateCount = await _context.Crates.CountAsync(c => c.UserId == userId);

        var crateLimit = SubscriptionLimits.GetCrateLimit(user.Plan);

        return crateCount < crateLimit;
    }

    public async Task<int> GetCrateCountAsync(string userId)
    {
        return await _context.Crates.CountAsync(c => c.UserId == userId);
    }

    public async Task<long> GetTotalUsedStorageAsync(string userId)
    {
        return await _context.FileObjects
            .Where(f => f.Crate.UserId == userId)
            .SumAsync(f => (long?)f.SizeInBytes) ?? 0;
    }

    public async Task<Result<Crate>> CreateCrateAsync(string userId, string name, string color)
    {
        var canCreate = await CanCreateCrateAsync(userId);
        if (!canCreate)
        {
            return Result<Crate>.Failure(Errors.CrateLimitReached);
        }

        var crate = new Crate
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserId = userId,
            Color = color
        };

        _context.Crates.Add(crate);
        await _context.SaveChangesAsync();
        return Result<Crate>.Success(crate);
    }

    public async Task<Result> DeleteCrateAsync(Guid crateId, string userId)
    {
        var crate = await _context.Crates
            .Include(c => c.Folders)
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);

        if (crate == null)
            return Result.Failure(Errors.CrateNotFound);

        _context.Crates.Remove(crate);
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<List<Crate>> GetCratesAsync(string userId)
    {
        return await _context.Crates
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    public async Task<Result<CrateUsageDto>> GetUsageAsync(Guid crateId, string userId)
    {
        var crate = await _context.Crates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);

        if (crate == null)
            return Result<CrateUsageDto>.Failure(Errors.CrateNotFound);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result<CrateUsageDto>.Failure(Errors.UserNotFound);

        var files = await _context.FileObjects
            .Where(f => f.CrateId == crateId)
            .ToListAsync();

        var usageDto = new CrateUsageDto
        {
            TotalUsed = (int)Math.Ceiling(files.Sum(f => f.SizeInBytes) / 1024.0 /
                                          1024.0), // Convert bytes to MB safely
            StorageLimit = SubscriptionLimits.GetStorageLimit(user.Plan),
            BreakdownByType = new Dictionary<string, int>()
        };

        foreach (var file in files)
        {
            var sizeMb = (int)Math.Ceiling((file.SizeInBytes) / 1024.0 / 1024.0);
            var category = MimeCategoryHelper.GetMimeCategory(file.MimeType ?? string.Empty);

            if (!usageDto.BreakdownByType.ContainsKey(category))
                usageDto.BreakdownByType[category] = 0;

            usageDto.BreakdownByType[category] += sizeMb;
        }

        return Result<CrateUsageDto>.Success(usageDto);
    }
}