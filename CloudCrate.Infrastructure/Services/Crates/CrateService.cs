using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateService : ICrateService
{
    private readonly IAppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStorageService _storageService;
    private readonly ICrateUserRoleService _crateUserRoleService;

    public CrateService(
        IAppDbContext context,
        UserManager<ApplicationUser> userManager,
        IStorageService storageService,
        ICrateUserRoleService crateUserRoleService)
    {
        _context = context;
        _userManager = userManager;
        _storageService = storageService;
        _crateUserRoleService = crateUserRoleService;
    }

    public async Task<Result<CrateResponse>> CreateCrateAsync(string userId, string name, string color)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result<CrateResponse>.Failure(Errors.User.NotFound);

        var canCreate = await CanCreateCrateAsync(userId, user.Plan);
        if (!canCreate)
            return Result<CrateResponse>.Failure(Errors.Crates.LimitReached);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var crate = Crate.Create(name, userId, color);
            _context.Crates.Add(crate);
            await _context.SaveChangesAsync();

            var assignRoleResult = await _crateUserRoleService.AssignRoleAsync(crate.Id, userId, CrateRole.Owner);
            if (!assignRoleResult.Succeeded)
                return await transaction.RollbackWithFailure<CrateResponse>(assignRoleResult.Errors);

            var storageResult = await _storageService.EnsureBucketExistsAsync(crate.GetCrateStorageName());
            if (!storageResult.Succeeded)
                return await transaction.RollbackWithFailure<CrateResponse>(storageResult.Errors);

            await transaction.CommitAsync();

            return Result<CrateResponse>.Success(new CrateResponse
            {
                Id = crate.Id,
                Name = crate.Name,
                Color = crate.Color
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result<CrateResponse>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }


    public async Task<Result<List<CrateResponse>>> GetCratesAsync(string userId)
    {
        try
        {
            var crates = await _context.Crates
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var responses = crates.Select(crate => new CrateResponse
            {
                Id = crate.Id,
                Name = crate.Name,
                Color = crate.Color
            }).ToList();

            return Result<List<CrateResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            return Result<List<CrateResponse>>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId)
    {
        try
        {
            var crate = await _context.Crates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);

            if (crate == null)
                return Result<CrateDetailsResponse>.Failure(Errors.Crates.NotFound);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<CrateDetailsResponse>.Failure(Errors.User.NotFound);

            var fileStats = await _context.FileObjects
                .Where(f => f.CrateId == crateId)
                .GroupBy(f => MimeCategoryHelper.GetMimeCategory(f.MimeType))
                .Select(g => new
                {
                    Category = g.Key,
                    TotalBytes = g.Sum(f => (long?)f.SizeInBytes) ?? 0
                })
                .ToListAsync();

            var totalBytes = fileStats.Sum(s => s.TotalBytes);
            var totalUsedMb = Math.Round(totalBytes / 1024.0 / 1024.0, 2);

            var breakdownByType = fileStats
                .Select(s => new FileTypeBreakdownDto
                {
                    Type = s.Category,
                    SizeMb = Math.Round(s.TotalBytes / 1024.0 / 1024.0, 2)
                })
                .ToList();

            var crateDetails = new CrateDetailsResponse
            {
                Id = crate.Id,
                Name = crate.Name,
                Color = crate.Color,
                TotalUsedStorage = totalUsedMb,
                StorageLimit = SubscriptionLimits.GetStorageLimit(user.Plan),
                BreakdownByType = breakdownByType
            };

            return Result<CrateDetailsResponse>.Success(crateDetails);
        }
        catch (Exception ex)
        {
            return Result<CrateDetailsResponse>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<int>> GetCrateCountAsync(string userId)
    {
        try
        {
            var count = await _context.Crates.CountAsync(c => c.UserId == userId);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<long>> GetTotalUsedStorageAsync(string userId)
    {
        try
        {
            var totalBytes = await _context.FileObjects
                .Where(f => f.Crate.UserId == userId)
                .SumAsync(f => (long?)f.SizeInBytes) ?? 0;

            return Result<long>.Success(totalBytes);
        }
        catch (Exception ex)
        {
            return Result<long>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }


    public async Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(
        Guid crateId,
        CrateMemberRequest request)
    {
        try
        {
            var memberUserIds = await _context.CrateUserRoles
                .Where(r => r.CrateId == crateId)
                .Select(r => r.UserId)
                .Distinct()
                .ToListAsync();

            if (memberUserIds.Count == 0)
                return Result<List<CrateMemberResponse>>.Success([]);

            var usersQuery = _userManager.Users
                .Where(u => memberUserIds.Contains(u.Id));

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var lowered = request.Email.Trim().ToLower();
                usersQuery = usersQuery.Where(u => u.Email.ToLower().Contains(lowered));
            }

            var pagedUsers = await usersQuery
                .OrderBy(u => u.Email)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            if (pagedUsers.Count == 0)
                return Result<List<CrateMemberResponse>>.Success([]);

            var pagedUserIds = pagedUsers.Select(u => u.Id).ToList();

            var roleMap = await _context.CrateUserRoles
                .Where(r => r.CrateId == crateId && pagedUserIds.Contains(r.UserId))
                .ToDictionaryAsync(r => r.UserId, r => r.Role);

            var result = pagedUsers
                .Select(user => new CrateMemberResponse
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Role = roleMap.GetValueOrDefault(user.Id, CrateRole.Viewer)
                })
                .ToList();

            return Result<List<CrateMemberResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<List<CrateMemberResponse>>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }


    public async Task<Result<CrateResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName,
        string? newColor)
    {
        var isOwnerResult = await _crateUserRoleService.IsOwnerAsync(crateId, userId);
        if (!isOwnerResult.Succeeded)
            return Result<CrateResponse>.Failure(isOwnerResult.Errors);

        if (!isOwnerResult.Value)
            return Result<CrateResponse>.Failure(Errors.User.Unauthorized);

        try
        {
            var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);
            if (crate == null)
                return Result<CrateResponse>.Failure(Errors.Crates.NotFound);

            if (!string.IsNullOrWhiteSpace(newName))
                crate.Rename(newName);

            if (!string.IsNullOrWhiteSpace(newColor))
                crate.SetColor(newColor);

            await _context.SaveChangesAsync();

            var dto = new CrateResponse
            {
                Id = crateId,
                Name = crate.Name,
                Color = crate.Color,
            };
            return Result<CrateResponse>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<CrateResponse>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result> DeleteCrateAsync(Guid crateId, string userId)
    {
        var isOwnerResult = await _crateUserRoleService.IsOwnerAsync(crateId, userId);
        if (!isOwnerResult.Succeeded)
            return Result.Failure(isOwnerResult.Errors);
        if (!isOwnerResult.Value)
            return Result.Failure(Errors.User.Unauthorized);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var crate = await _context.Crates
                .Include(c => c.Files)
                .Include(c => c.Folders)
                .ThenInclude(f => f.Files)
                .Include(c => c.Folders)
                .ThenInclude(f => f.Subfolders)
                .FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);

            if (crate == null)
                return Result.Failure(Errors.Crates.NotFound);

            var keysToDelete = new List<string>();

            foreach (var file in crate.Files)
                _context.FileObjects.Remove(file);

            foreach (var folder in crate.Folders.Where(f => f.ParentFolderId == null))
                await CollectFolderDeletionsAsync(folder, crate.Id, userId, keysToDelete);

            _context.Folders.RemoveRange(crate.Folders);
            _context.Crates.Remove(crate);

            await _context.SaveChangesAsync();

            var deleteFilesResult = await _storageService.DeleteAllFilesInBucketAsync(crateId);
            if (!deleteFilesResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return Result.Failure(deleteFilesResult.Errors);
            }

            var bucketDeleteResult = await _storageService.DeleteBucketAsync(crateId);
            if (!bucketDeleteResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return Result.Failure(bucketDeleteResult.Errors);
            }

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }


    private async Task CollectFolderDeletionsAsync(
        Domain.Entities.Folder folder,
        Guid crateId,
        string userId,
        List<string> keysToDelete)
    {
        foreach (var file in folder.Files)
        {
            var key = userId.GetObjectKey(crateId, folder.Id, file.Name);
            keysToDelete.Add(key);
            _context.FileObjects.Remove(file);
        }

        foreach (var subfolder in folder.Subfolders)
        {
            await CollectFolderDeletionsAsync(subfolder, crateId, userId, keysToDelete);
        }
    }

    private async Task<bool> CanCreateCrateAsync(string userId, SubscriptionPlan subscriptionPlan)
    {
        var crateLimit = SubscriptionLimits.GetCrateLimit(subscriptionPlan);
        var crateCount = await _context.Crates.CountAsync(c => c.UserId == userId);
        return crateCount < crateLimit;
    }
}