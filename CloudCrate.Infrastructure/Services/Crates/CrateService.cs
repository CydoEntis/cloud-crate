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
using CloudCrate.Application.Interfaces.Permissions;
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
    private readonly ICrateRoleService _crateRoleService;

    public CrateService(
        IAppDbContext context,
        UserManager<ApplicationUser> userManager,
        IUserService userService,
        IStorageService storageService,
        ICrateMemberService crateMemberService,
        ICrateRoleService crateRoleService)
    {
        _context = context;
        _userManager = userManager;
        _userService = userService;
        _storageService = storageService;
        _crateMemberService = crateMemberService;
        _crateRoleService = crateRoleService;
    }

    public async Task<Result<Guid>> CreateCrateAsync(string userId, string name, string color, int storageAllocationGB)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<Guid>.Failure(new NotFoundError("User not found"));

        var canAllocateResult = await _userService.CanConsumeStorageAsync(userId, Crate.GbToBytes(storageAllocationGB));
        if (!canAllocateResult.IsSuccess)
            return Result<Guid>.Failure(canAllocateResult.Error ??
                                        new InternalError("Storage allocation check failed"));


        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var crate = Crate.Create(name, userId, color);

            if (!crate.TryAllocateStorageGB(storageAllocationGB, out var allocationError))
                return Result<Guid>.Failure(new StorageError("Storage allocation amount exceeded"));

            _context.Crates.Add(crate);
            await _context.SaveChangesAsync();

            var storageResult = await _storageService.EnsureBucketExistsAsync(crate.GetCrateStorageName());
            if (!storageResult.IsSuccess)
                return Result<Guid>.Failure(storageResult.Error ??
                                            new InternalError("Failed to create storage bucket"));

            var rootFolder = CrateFolder.CreateRoot("Root", crate.Id, null, null, userId);
            _context.CrateFolders.Add(rootFolder);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Result<Guid>.Success(crate.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result<Guid>.Failure(new InternalError($"Failed to create crate: {ex.Message}"));
        }
    }

    public async Task<Result<bool>> AllocateCrateStorageAsync(string userId, Guid crateId, int requestedAllocationGB)
    {
        if (requestedAllocationGB < 0)
            return Result<bool>.Failure(new ValidationError("Requested allocation must be >= 0"));

        var member = await _context.CrateMembers.FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);
        if (member is null || member.Role != CrateRole.Owner)
            return Result<bool>.Failure(new ForbiddenError("Only crate owners can allocate storage"));

        var userResult = await _userService.GetUserByIdAsync(userId);
        if (!userResult.IsSuccess)
            return Result<bool>.Failure(userResult.Error ?? new InternalError("Failed to retrieve user"));

        var user = userResult.Value!;
        long requestedBytes = Crate.GbToBytes(requestedAllocationGB);
        long remainingBytes = user.MaxStorageBytes - user.UsedStorageBytes;

        if (requestedBytes > remainingBytes)
            return Result<bool>.Failure(new StorageError("Requested allocation exceeds remaining quota"));

        var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate is null)
            return Result<bool>.Failure(new NotFoundError("Crate not found"));

        if (!crate.TryAllocateStorageGB(requestedAllocationGB, out var error))
            return Result<bool>.Failure(new StorageError(error ?? "Failed to allocate storage"));

        _context.Crates.Update(crate);
        await _context.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<PaginatedResult<CrateResponse>>> GetCratesAsync(CrateQueryParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.UserId))
            return Result<PaginatedResult<CrateResponse>>.Failure(
                Error.Unauthorized("User must be logged in to access this resource"));

        try
        {
            var query = _context.Crates
                .Include(c => c.Members)
                .Include(c => c.Files)
                .Where(c => c.Members.Any(m => m.UserId == parameters.UserId));

            query = parameters.MemberType switch
            {
                CrateMemberType.Owner => query.Where(c =>
                    c.Members.Any(m => m.UserId == parameters.UserId && m.Role == CrateRole.Owner)),
                CrateMemberType.Joined => query.Where(c =>
                    c.Members.Any(m => m.UserId == parameters.UserId && m.Role != CrateRole.Owner)),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var term = parameters.SearchTerm.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(term));
            }

            query = (parameters.SortBy, parameters.Ascending) switch
            {
                (CrateSortBy.Name, true) => query.OrderBy(c => c.Name),
                (CrateSortBy.Name, false) => query.OrderByDescending(c => c.Name),
                (CrateSortBy.JoinedAt, true) => query.OrderBy(c =>
                    c.Members.FirstOrDefault(m => m.UserId == parameters.UserId)!.JoinedDate),
                (CrateSortBy.JoinedAt, false) => query.OrderByDescending(c =>
                    c.Members.FirstOrDefault(m => m.UserId == parameters.UserId)!.JoinedDate),
                (CrateSortBy.UsedStorage, true) => query.OrderBy(c => c.Files.Sum(f => f.SizeInBytes)),
                (CrateSortBy.UsedStorage, false) => query.OrderByDescending(c => c.Files.Sum(f => f.SizeInBytes)),
                _ => parameters.Ascending ? query.OrderBy(c => c.Name) : query.OrderByDescending(c => c.Name)
            };

            var totalCount = await query.CountAsync();
            var pagedCrates = await query.Skip((parameters.Page - 1) * parameters.PageSize)
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
                    }
                };
            }).ToList();

            return Result<PaginatedResult<CrateResponse>>.Success(
                PaginatedResult<CrateResponse>.Create(crateResponses, totalCount, parameters.Page, parameters.PageSize)
            );
        }
        catch (Exception ex)
        {
            return Result<PaginatedResult<CrateResponse>>.Failure(
                new InternalError($"Failed to get crates: {ex.Message}"));
        }
    }

    public async Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId)
    {
        try
        {
            var permissionResult = await _crateRoleService.CanView(crateId, userId);
            if (!permissionResult.IsSuccess || !permissionResult.Value)
                return Result<CrateDetailsResponse>.Failure(new UnauthorizedError("User cannot view this crate"));

            var member = await _context.CrateMembers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);
            if (member is null)
                return Result<CrateDetailsResponse>.Failure(new NotFoundError("Crate member not found"));

            var crate = await _context.Crates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == crateId);
            if (crate is null)
                return Result<CrateDetailsResponse>.Failure(new NotFoundError("Crate not found"));

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return Result<CrateDetailsResponse>.Failure(new NotFoundError("User not found"));

            var groupedByMimeType = await _context.FileObjects
                .Where(f => f.CrateId == crateId)
                .GroupBy(f => f.MimeType)
                .Select(g => new { MimeType = g.Key, TotalBytes = g.Sum(f => (long?)f.SizeInBytes) ?? 0 })
                .ToListAsync();

            var fileStats = groupedByMimeType
                .GroupBy(g => MimeCategoryHelper.GetMimeCategory(g.MimeType))
                .Select(g => new { Category = g.Key, TotalBytes = g.Sum(x => x.TotalBytes) })
                .ToList();

            var totalBytes = fileStats.Sum(s => s.TotalBytes);
            var breakdownByType = fileStats.Select(s => new FileTypeBreakdownDto
            {
                Type = s.Category,
                SizeMb = Math.Round(s.TotalBytes / 1024.0 / 1024.0, 2)
            }).ToList();

            var crateDetails = new CrateDetailsResponse
            {
                Id = crate.Id,
                Name = crate.Name,
                Role = member.Role,
                Color = crate.Color,
                TotalUsedStorage = Math.Round(totalBytes / 1024.0 / 1024.0, 2),
                StorageLimit = crate.AllocatedStorageBytes,
                BreakdownByType = breakdownByType
            };

            return Result<CrateDetailsResponse>.Success(crateDetails);
        }
        catch (Exception ex)
        {
            return Result<CrateDetailsResponse>.Failure(
                new InternalError($"Failed to get crate details: {ex.Message}"));
        }
    }

    public async Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(Guid crateId, CrateMemberRequest request)
    {
        try
        {
            var canView = await _crateRoleService.CanView(crateId, request.UserId);
            if (!canView.IsSuccess || !canView.Value)
                return Result<List<CrateMemberResponse>>.Failure(
                    new UnauthorizedError("User cannot view crate members"));

            var memberUserIds = await _context.CrateMembers.Where(r => r.CrateId == crateId).Select(r => r.UserId)
                .Distinct().ToListAsync();
            if (!memberUserIds.Any())
                return Result<List<CrateMemberResponse>>.Success(new List<CrateMemberResponse>());

            var usersQuery = _userManager.Users.Where(u => memberUserIds.Contains(u.Id));
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var lowered = request.Email.Trim().ToLower();
                usersQuery = usersQuery.Where(u => u.Email.ToLower().Contains(lowered));
            }

            var pagedUsers = await usersQuery.OrderBy(u => u.Email)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            if (!pagedUsers.Any())
                return Result<List<CrateMemberResponse>>.Success(new List<CrateMemberResponse>());

            var roleMap = await _context.CrateMembers
                .Where(r => r.CrateId == crateId && pagedUsers.Select(u => u.Id).Contains(r.UserId))
                .ToDictionaryAsync(r => r.UserId, r => r.Role);

            var result = pagedUsers.Select(u => new CrateMemberResponse
            {
                UserId = u.Id,
                Email = u.Email,
                Role = roleMap.GetValueOrDefault(u.Id, CrateRole.Viewer)
            }).ToList();

            return Result<List<CrateMemberResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<List<CrateMemberResponse>>.Failure(
                new InternalError($"Failed to get crate members: {ex.Message}"));
        }
    }

    public async Task<Result<CrateResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName,
        string? newColor)
    {
        var canManage = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManage.IsSuccess || !canManage.Value)
            return Result<CrateResponse>.Failure(new UnauthorizedError("User cannot manage crate"));

        try
        {
            var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
            if (crate is null)
                return Result<CrateResponse>.Failure(new NotFoundError("Crate not found"));

            if (!string.IsNullOrWhiteSpace(newName)) crate.Rename(newName);
            if (!string.IsNullOrWhiteSpace(newColor)) crate.SetColor(newColor);

            await _context.SaveChangesAsync();

            return Result<CrateResponse>.Success(new CrateResponse
                { Id = crateId, Name = crate.Name, Color = crate.Color });
        }
        catch (Exception ex)
        {
            return Result<CrateResponse>.Failure(new InternalError($"Failed to update crate: {ex.Message}"));
        }
    }

    public async Task<Result> DeleteCrateAsync(Guid crateId, string userId)
    {
        var canManage = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManage.IsSuccess || !canManage.Value)
            return Result.Failure(new UnauthorizedError("User cannot manage crate"));

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var crate = await _context.Crates
                .Include(c => c.Files)
                .Include(c => c.Folders).ThenInclude(f => f.Files)
                .Include(c => c.Folders).ThenInclude(f => f.Subfolders)
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.Id == crateId);

            if (crate is null)
                return Result.Failure(new NotFoundError("Crate not found"));

            foreach (var file in crate.Files) _context.FileObjects.Remove(file);
            foreach (var folder in crate.Folders.Where(f => f.ParentFolderId == null))
                await CollectFolderDeletionsAsync(folder, crate.Id, userId, new List<string>());

            _context.CrateMembers.RemoveRange(crate.Members);
            _context.Folders.RemoveRange(crate.Folders);
            _context.Crates.Remove(crate);
            await _context.SaveChangesAsync();

            var deleteFilesResult = await _storageService.DeleteAllFilesInBucketAsync(crateId);
            if (!deleteFilesResult.IsSuccess)
                return Result.Failure(deleteFilesResult.Error ?? new InternalError("Failed to delete files"));

            var bucketDeleteResult = await _storageService.DeleteBucketAsync(crateId);
            if (!bucketDeleteResult.IsSuccess)
                return Result.Failure(bucketDeleteResult.Error ?? new InternalError("Failed to delete bucket"));

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result.Failure(new InternalError($"Failed to delete crate: {ex.Message}"));
        }
    }

    public async Task<Result> LeaveCrateAsync(Guid crateId, string userId)
    {
        var member = await _context.CrateMembers.FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);
        if (member is null) return Result.Failure(new ForbiddenError("User is not a member of this crate"));
        if (member.Role == CrateRole.Owner) return Result.Failure(new ForbiddenError("Owner cannot leave crate"));

        _context.CrateMembers.Remove(member);
        await _context.SaveChangesAsync();

        return Result.Success();
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

        foreach (var subfolder in folder.Subfolders)
            await CollectFolderDeletionsAsync(subfolder, crateId, userId, keysToDelete);
    }
}