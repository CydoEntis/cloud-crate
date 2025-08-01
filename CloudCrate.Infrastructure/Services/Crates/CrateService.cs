using CloudCrate.Application.Common.Constants;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
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
    private readonly IUserService _userService;
    private readonly IStorageService _storageService;
    private readonly ICrateMemberService _crateMemberService;

    public CrateService(
        IAppDbContext context,
        UserManager<ApplicationUser> userManager,
        IUserService userService,
        IStorageService storageService,
        ICrateMemberService crateMemberService)
    {
        _context = context;
        _userManager = userManager;
        _userService = userService;
        _storageService = storageService;
        _crateMemberService = crateMemberService;
    }

    public async Task<Result<CrateResponse>> CreateCrateAsync(string userId, string name, string color)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result<CrateResponse>.Failure(Errors.User.NotFound);

        var canCreate = await _userService.CanCreateCrateAsync(userId);
        if (!canCreate.Succeeded)
            return Result<CrateResponse>.Failure(Errors.Crates.LimitReached);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var crate = Crate.Create(name, userId, color);
            _context.Crates.Add(crate);
            await _context.SaveChangesAsync();

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

    public async Task<Result<PaginatedResult<CrateResponse>>> GetCratesAsync(CrateQueryParameters parameters)
    {
        try
        {
            var query = _context.Crates
                .Include(c => c.Members)
                .Include(c => c.Files)
                .AsQueryable();

            if (!string.IsNullOrEmpty(parameters.UserId))
            {
                query = query.Where(c => c.Members.Any(m => m.UserId == parameters.UserId));
            }
            else
            {
                return Result<PaginatedResult<CrateResponse>>.Failure(Errors.User.Unauthorized with
                {
                    Message = "UserId is required"
                });
            }

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var term = parameters.SearchTerm.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(term));
            }

            if (parameters.SortBy.HasValue)
            {
                if (parameters.SortBy == CrateSortBy.Owned)
                {
                    query = query.Where(c =>
                        c.Members.Any(m => m.UserId == parameters.UserId && m.Role == CrateRole.Owner));
                }
                else if (parameters.SortBy == CrateSortBy.Joined)
                {
                    query = query.Where(c =>
                        c.Members.Any(m => m.UserId == parameters.UserId) &&
                        !c.Members.Any(m => m.UserId == parameters.UserId && m.Role == CrateRole.Owner));
                }
                else
                {
                    bool descending = parameters.OrderBy == OrderBy.Desc;

                    switch (parameters.SortBy.Value)
                    {
                        case CrateSortBy.Name:
                            query = descending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name);
                            break;

                        case CrateSortBy.JoinedAt:
                            query = descending
                                ? query.OrderByDescending(c =>
                                    c.Members.FirstOrDefault(m => m.UserId == parameters.UserId)!.JoinedDate)
                                : query.OrderBy(c =>
                                    c.Members.FirstOrDefault(m => m.UserId == parameters.UserId)!.JoinedDate);
                            break;

                        case CrateSortBy.UsedStorage:
                            query = descending
                                ? query.OrderByDescending(c => c.Files.Sum(f => f.SizeInBytes))
                                : query.OrderBy(c => c.Files.Sum(f => f.SizeInBytes));
                            break;
                    }
                }
            }
            else
            {
                query = query.OrderBy(c => c.Name);
            }

            var totalCount = await query.CountAsync();

            var pagedCrates = await query
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync();

            var ownerUserIds = pagedCrates
                .Select(c => c.Members.FirstOrDefault(m => m.Role == CrateRole.Owner)?.UserId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            var ownerProfiles = await _userService.GetUsersByIdsAsync(ownerUserIds);

            var crateResponses = pagedCrates.Select(crate =>
            {
                var ownerMember = crate.Members.FirstOrDefault(m => m.Role == CrateRole.Owner);
                var ownerProfile = ownerProfiles.FirstOrDefault(p => p.Id == ownerMember?.UserId);
                var currentUserMembership = crate.Members.FirstOrDefault(m => m.UserId == parameters.UserId);

                return new CrateResponse
                {
                    Id = crate.Id,
                    Name = crate.Name,
                    Color = crate.Color,
                    UsedStorage = crate.Files.Sum(f => f.SizeInBytes),
                    JoinedAt = currentUserMembership!.JoinedDate,
                    Owner = new CrateMemberResponse
                    {
                        UserId = ownerMember!.UserId,
                        DisplayName = ownerProfile!.DisplayName,
                        Email = ownerProfile!.Email,
                        Role = CrateRole.Owner,
                        ProfilePicture = ownerProfile!.ProfilePictureUrl
                    },
                };
            }).ToList();

            return Result<PaginatedResult<CrateResponse>>.Success(
                PaginatedResult<CrateResponse>.Create(crateResponses, totalCount, parameters.Page, parameters.PageSize)
            );
        }
        catch (Exception ex)
        {
            return Result<PaginatedResult<CrateResponse>>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"Internal server error ({ex.Message})"
            });
        }
    }


    public async Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId)
    {
        try
        {
            var member = await _context.CrateMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

            if (member is null)
                return Result<CrateDetailsResponse>.Failure(Errors.Crates.NotFound);


            var crate = await _context.Crates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == crateId);

            if (crate == null)
                return Result<CrateDetailsResponse>.Failure(Errors.Crates.NotFound);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Result<CrateDetailsResponse>.Failure(Errors.User.NotFound);


            var groupedByMimeType = await _context.FileObjects
                .Where(f => f.CrateId == crateId)
                .GroupBy(f => f.MimeType)
                .Select(g => new
                {
                    MimeType = g.Key,
                    TotalBytes = g.Sum(f => (long?)f.SizeInBytes) ?? 0
                })
                .ToListAsync();

            var fileStats = groupedByMimeType
                .GroupBy(g => MimeCategoryHelper.GetMimeCategory(g.MimeType))
                .Select(g => new
                {
                    Category = g.Key,
                    TotalBytes = g.Sum(x => x.TotalBytes)
                })
                .ToList();

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
                Role = member.Role,
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


    public async Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(
        Guid crateId,
        CrateMemberRequest request)
    {
        try
        {
            var memberUserIds = await _context.CrateMembers
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

            var roleMap = await _context.CrateMembers
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


    public async Task<Result<CrateResponse>> UpdateCrateAsync(
        Guid crateId,
        string userId,
        string? newName,
        string? newColor)
    {
        var isOwnerResult = await _crateMemberService.IsOwnerAsync(crateId, userId);
        if (!isOwnerResult.Succeeded)
            return Result<CrateResponse>.Failure(isOwnerResult.Errors);

        if (!isOwnerResult.Value)
            return Result<CrateResponse>.Failure(Errors.User.Unauthorized);

        try
        {
            var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
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
        var isOwnerResult = await _crateMemberService.IsOwnerAsync(crateId, userId);
        if (!isOwnerResult.Succeeded)
            return Result.Failure(isOwnerResult.Errors);
        if (!isOwnerResult.Value)
            return Result.Failure(Errors.User.Unauthorized);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var crate = await _context.Crates
                .Include(c => c.Files)
                .Include(c => c.Folders).ThenInclude(f => f.Files)
                .Include(c => c.Folders).ThenInclude(f => f.Subfolders)
                .Include(c => c.Members) // ✅ Include members to delete them
                .FirstOrDefaultAsync(c => c.Id == crateId);

            if (crate == null)
                return Result.Failure(Errors.Crates.NotFound);

            var keysToDelete = new List<string>();

            foreach (var file in crate.Files)
                _context.FileObjects.Remove(file);

            foreach (var folder in crate.Folders.Where(f => f.ParentFolderId == null))
                await CollectFolderDeletionsAsync(folder, crate.Id, userId, keysToDelete);

            _context.CrateMembers.RemoveRange(crate.Members);

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

    public async Task<Result> LeaveCrateAsync(Guid crateId, string userId)
    {
        var member = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (member is null)
            return Result.Failure(Errors.User.Unauthorized);

        if (member.Role == CrateRole.Owner)
            return Result.Failure(Errors.User.OwnerRoleRemovalNotAllowed);

        _context.CrateMembers.Remove(member);
        await _context.SaveChangesAsync();

        return Result.Success();
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
}