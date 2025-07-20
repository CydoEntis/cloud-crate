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

    public async Task<Result<bool>> CanCreateCrateAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<bool>.Failure(Errors.User.NotFound);

            var crateCount = await _context.Crates.CountAsync(c => c.UserId == userId);
            var crateLimit = SubscriptionLimits.GetCrateLimit(user.Plan);

            return Result<bool>.Success(crateCount < crateLimit);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(Errors.Common.InternalServerError with
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

    public async Task<Result<CrateResponse>> CreateCrateAsync(string userId, string name, string color)
    {
        var canCreateResult = await CanCreateCrateAsync(userId);
        if (!canCreateResult.Succeeded)
            return Result<CrateResponse>.Failure(canCreateResult.Errors);

        if (!canCreateResult.Value)
            return Result<CrateResponse>.Failure(Errors.Crates.LimitReached);

        try
        {
            var crate = Crate.Create(name, userId, color);

            _context.Crates.Add(crate);
            await _context.SaveChangesAsync();

            var assignRoleResult = await _crateUserRoleService.AssignRoleAsync(crate.Id, userId, CrateRole.Owner);
            if (!assignRoleResult.Succeeded)
                return Result<CrateResponse>.Failure(assignRoleResult.Errors);

            var bucketName = $"crate-{crate.Id}".ToLowerInvariant();
            var storageResult = await _storageService.EnsureBucketExistsAsync(bucketName);
            if (!storageResult.Succeeded)
                return Result<CrateResponse>.Failure(storageResult.Errors);

            var response = new CrateResponse
            {
                Id = crate.Id,
                Name = crate.Name,
                Color = crate.Color
            };

            return Result<CrateResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<CrateResponse>.Failure(Errors.Common.InternalServerError with
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
        // Fix: safely unwrap nullable bool with GetValueOrDefault()
        if (!isOwnerResult.Value)
            return Result<CrateResponse>.Failure(Errors.User.Unauthorized);

        try
        {
            var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId && c.UserId == userId);
            if (crate == null)
                return Result<CrateResponse>.Failure(Errors.Crates.NotFound);

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
        // Fix: safely unwrap nullable bool with GetValueOrDefault()
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

            // Delete all files from bucket first
            if (keysToDelete.Any())
            {
                var deleteResult = await _storageService.DeleteFilesAsync(bucketName, keysToDelete);
                if (!deleteResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure(deleteResult.Errors);
                }
            }

            // Delete the entire bucket itself now
            var bucketDeleteResult = await _storageService.DeleteBucketAsync(bucketName);
            if (!bucketDeleteResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return Result.Failure(bucketDeleteResult.Errors);
            }

            await _context.SaveChangesAsync();
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

    public async Task<Result<List<Crate>>> GetCratesAsync(string userId)
    {
        try
        {
            var crates = await _context.Crates
                .Where(c => c.UserId == userId).OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Result<List<Crate>>.Success(crates);
        }
        catch (Exception ex)
        {
            return Result<List<Crate>>.Failure(Errors.Common.InternalServerError with
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
        catch (Exception ex)
        {
            return Result<CrateDetailsResponse>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    private async Task CollectFolderDeletionsAsync(Domain.Entities.Folder folder, Guid crateId, string userId,
        List<string> keysToDelete)
    {
        foreach (var file in folder.Files)
        {
            var key = userId.GetObjectKey(crateId, folder.Id, file.Name);
            keysToDelete.Add(key);
            _context.FileObjects.Remove(file);
        }

        var subfolders = folder.Subfolders?.ToList() ?? new List<Domain.Entities.Folder>();
        foreach (var subfolder in subfolders)
        {
            await CollectFolderDeletionsAsync(subfolder, crateId, userId, keysToDelete);
        }

        _context.Folders.Remove(folder);
    }

    public async Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(
        Guid crateId,
        CrateMemberRequest request)
    {
        try
        {
            var rolesQuery = _context.CrateUserRoles
                .Where(r => r.CrateId == crateId);

            var userIdsQuery = rolesQuery
                .Select(r => r.UserId)
                .Distinct();

            var usersQuery = _userManager.Users
                .Where(u => userIdsQuery.Contains(u.Id));

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
            {
                return Result<List<CrateMemberResponse>>.Success(new List<CrateMemberResponse>());
            }

            var userIds = pagedUsers.Select(u => u.Id).ToList();

            var roles = await rolesQuery
                .Where(r => userIds.Contains(r.UserId))
                .ToListAsync();

            var result = pagedUsers
                .Select(user =>
                {
                    var role = roles.FirstOrDefault(r => r.UserId == user.Id);
                    return new CrateMemberResponse
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        Role = role?.Role ?? CrateRole.Viewer
                    };
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
}