using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
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
    private readonly IStorageService _storageService;
    private readonly ICrateUserRoleService _crateUserRoleService;

    public CrateService(IAppDbContext context, UserManager<ApplicationUser> userManager, IStorageService storageService,
        ICrateUserRoleService crateUserRoleService)
    {
        _context = context;
        _userManager = userManager;
        _storageService = storageService;
        _crateUserRoleService = crateUserRoleService;
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

        var crate = Crate.Create(name, userId, color);

        _context.Crates.Add(crate);
        await _context.SaveChangesAsync();

        await _crateUserRoleService.AssignRoleAsync(crate.Id, userId, CrateRole.Owner);

        return Result<Crate>.Success(crate);
    }

    public async Task<Result<CrateResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName,
        string? newColor)
    {
        if (!await _crateUserRoleService.IsOwnerAsync(crateId, userId))
            return Result<CrateResponse>.Failure(Errors.Unauthorized);


        var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);
        if (crate == null)
            return Result<CrateResponse>.Failure(Errors.CrateNotFound);

        if (!string.IsNullOrWhiteSpace(newName))
            crate.Name = newName;

        if (!string.IsNullOrWhiteSpace(newColor))
            crate.Color = newColor;

        await _context.SaveChangesAsync();


        var dto = new CrateResponse
        {
            Id = crateId,
            Name = crate.Name,
            Color = crate.Color,
        };
        return Result<CrateResponse>.Success(dto);
    }


    public async Task<Result> DeleteCrateAsync(Guid crateId, string userId)
    {
        if (!await _crateUserRoleService.IsOwnerAsync(crateId, userId))
            return Result<CrateResponse>.Failure(Errors.Unauthorized);


        using var transaction = await _context.Database.BeginTransactionAsync();

        var crate = await _context.Crates
            .Include(c => c.Files)
            .Include(c => c.Folders)
            .ThenInclude(f => f.Files)
            .Include(c => c.Folders)
            .ThenInclude(f => f.Subfolders)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);

        if (crate == null)
            return Result.Failure(Errors.CrateNotFound);

        var bucketName = $"crate-{crate.Id}".ToLowerInvariant();
        var keysToDelete = new List<string>();

        foreach (var file in crate.Files)
        {
            var key = userId.GetObjectKey(crateId, null, file.Name);
            keysToDelete.Add(key);
            _context.FileObjects.Remove(file);
        }

        foreach (var folder in crate.Folders.Where(f => f.ParentFolderId == null))
        {
            await CollectFolderDeletionsAsync(folder, crate.Id, userId, keysToDelete);
        }

        _context.Folders.RemoveRange(crate.Folders);

        _context.Crates.Remove(crate);

        var deleteResult = await _storageService.DeleteFilesAsync(bucketName, keysToDelete);
        if (!deleteResult.Succeeded)
        {
            await transaction.RollbackAsync();
            return deleteResult;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result.Success();
    }

    public async Task<List<Crate>> GetCratesAsync(string userId)
    {
        return await _context.Crates
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    public async Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId)
    {
        var crate = await _context.Crates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);

        if (crate == null)
            return Result<CrateDetailsResponse>.Failure(Errors.CrateNotFound);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result<CrateDetailsResponse>.Failure(Errors.UserNotFound);

        var files = await _context.FileObjects
            .Where(f => f.CrateId == crateId)
            .ToListAsync();

        var totalBytes = files.Sum(f => f.SizeInBytes);
        var totalUsedMb = Math.Round(totalBytes / 1024.0 / 1024.0, 2);

        var breakdownMap = new Dictionary<string, double>();
        foreach (var group in files.GroupBy(f => MimeCategoryHelper.GetMimeCategory(f.MimeType ?? string.Empty)))
        {
            var groupBytes = group.Sum(f => f.SizeInBytes);
            var groupSizeMb = Math.Round(groupBytes / 1024.0 / 1024.0, 2);
            breakdownMap[group.Key] = groupSizeMb;
        }

        var usageDto = new CrateDetailsResponse
        {
            Id = crate.Id,
            Name = crate.Name,
            Color = crate.Color,
            TotalUsedStorage = totalUsedMb,
            StorageLimit = SubscriptionLimits.GetStorageLimit(user.Plan),
            BreakdownByType = breakdownMap
                .Select(pair => new FileTypeBreakdownDto
                {
                    Type = pair.Key,
                    SizeMb = pair.Value
                })
                .ToList()
        };

        return Result<CrateDetailsResponse>.Success(usageDto);
    }

    private async Task CollectFolderDeletionsAsync(Folder folder, Guid crateId, string userId,
        List<string> keysToDelete)
    {
        foreach (var file in folder.Files)
        {
            var key = userId.GetObjectKey(crateId, folder.Id, file.Name);
            keysToDelete.Add(key);
            _context.FileObjects.Remove(file);
        }

        var subfolders = folder.Subfolders?.ToList() ?? new();
        foreach (var subfolder in subfolders)
        {
            await CollectFolderDeletionsAsync(subfolder, crateId, userId, keysToDelete);
        }

        _context.Folders.Remove(folder);
    }
}