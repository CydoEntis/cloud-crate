using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
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

        return user.Plan switch
        {
            SubscriptionPlan.Free => crateCount < 1,
            SubscriptionPlan.Pro => crateCount < 5,
            _ => false
        };
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

    public async Task<Result<Crate>> CreateCrateAsync(string userId, string name)
    {
        var canCreate = await CanCreateCrateAsync(userId);
        if (!canCreate)
        {
            return Result<Crate>.Failure("Limit", "Crate limit reached for your subscription plan.");
        }

        var crate = new Crate
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserId = userId
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
            return Result.Failure("NotFound", "Crate not found or access denied.");

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
}