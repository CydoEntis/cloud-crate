using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Folder;

public class FolderService : IFolderService
{
    private readonly IAppDbContext _context;
    private readonly ICratePermissionService _cratePermissionService;
    private readonly IUserService _userService;

    public FolderService(
        IAppDbContext context,
        ICratePermissionService cratePermissionService, IUserService userService)
    {
        _context = context;
        _cratePermissionService = cratePermissionService;
        _userService = userService;
    }

    public async Task<Result<FolderResponse>> CreateFolderAsync(CreateFolderRequest request, string userId)
    {
        var editPermission = await _cratePermissionService.CheckUploadPermissionAsync(request.CrateId, userId);
        if (!editPermission.Succeeded)
            return Result<FolderResponse>.Failure(editPermission.Errors);

        if (request.ParentFolderId.HasValue)
        {
            var parentExists =
                await _context.Folders.AnyAsync(f => f.Id == request.ParentFolderId && f.CrateId == request.CrateId);
            if (!parentExists)
                return Result<FolderResponse>.Failure(Errors.Folders.NotFound);
        }

        var user = await _userService.GetUserByIdAsync(userId);

        var folder = Domain.Entities.Folder.Create(
            request.Name,
            request.CrateId,
            request.ParentFolderId,
            request.Color,
            userId,
            user?.DisplayName ?? "Unknown",
            user?.Email ?? string.Empty,
            user?.ProfilePictureUrl ?? string.Empty
        );

        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();

        return Result<FolderResponse>.Success(new FolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            CrateId = folder.CrateId,
            ParentFolderId = folder.ParentFolderId,
            Color = folder.Color,
            UploadedByUserId = folder.UploadedByUserId,
            UploadedByDisplayName = folder.UploadedByDisplayName,
            UploadedByEmail = folder.UploadedByEmail,
            UploadedByProfilePictureUrl = folder.UploadedByProfilePictureUrl,
            CreatedAt = folder.CreatedAt
        });
    }


    public async Task<Result> RenameFolderAsync(Guid folderId, string newName, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null)
            return Result.Failure(Errors.Folders.NotFound);

        var editPermission = await _cratePermissionService.CheckUploadPermissionAsync(folder.CrateId, userId);
        if (!editPermission.Succeeded)
            return Result.Failure(editPermission.Errors);

        folder.Name = newName;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .Include(f => f.Subfolders)
            .Include(f => f.Files)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null)
            return Result.Failure(Errors.Folders.NotFound);

        var ownerPermission = await _cratePermissionService.CheckOwnerPermissionAsync(folder.CrateId, userId);
        if (!ownerPermission.Succeeded)
            return Result.Failure(ownerPermission.Errors);

        if (folder.Subfolders.Any() || folder.Files.Any())
            return Result.Failure(Errors.Folders.NotEmpty);

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null)
            return Result.Failure(Errors.Folders.NotFound);

        var editPermission = await _cratePermissionService.CheckUploadPermissionAsync(folder.CrateId, userId);
        if (!editPermission.Succeeded)
            return Result.Failure(editPermission.Errors);

        if (newParentId.HasValue)
        {
            var newParent = await _context.Folders
                .Include(f => f.Crate)
                .FirstOrDefaultAsync(f => f.Id == newParentId.Value);

            if (newParent == null || newParent.CrateId != folder.CrateId)
                return Result.Failure(Errors.Folders.NotFound);

            if (newParentId == folder.Id)
                return Result.Failure(Errors.Folders.InvalidMove);

            Guid? currentParentId = newParentId;
            while (currentParentId != null)
            {
                if (currentParentId == folder.Id)
                    return Result.Failure(Errors.Folders.InvalidMove);

                currentParentId = await _context.Folders
                    .Where(f => f.Id == currentParentId)
                    .Select(f => f.ParentFolderId)
                    .FirstOrDefaultAsync();
            }
        }

        folder.ParentFolderId = newParentId;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FolderContentsResponse>> GetFolderContentsAsync(
        Guid crateId,
        Guid? parentFolderId,
        string userId,
        string? search,
        int page = 1,
        int pageSize = 20)
    {
        var viewPermission = await _cratePermissionService.CheckViewPermissionAsync(crateId, userId);
        if (!viewPermission.Succeeded)
            return Result<FolderContentsResponse>.Failure(viewPermission.Errors);

        var foldersQuery = _context.Folders
            .Where(f => f.CrateId == crateId && f.ParentFolderId == parentFolderId);

        var filesQuery = _context.FileObjects
            .Where(f => f.CrateId == crateId && f.FolderId == parentFolderId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            foldersQuery = foldersQuery.Where(f => EF.Functions.ILike(f.Name, $"%{search}%"));
            filesQuery = filesQuery.Where(f => EF.Functions.ILike(f.Name, $"%{search}%"));
        }

        // Load entities first so we can enrich consistently
        var foldersList = await foldersQuery.ToListAsync();
        var filesList = await filesQuery.ToListAsync();

        // Collect uploader ids from BOTH folders and files
        var uploaderIds = foldersList
            .Select(f => f.UploadedByUserId)
            .Concat(filesList.Select(f => f.UploadedByUserId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var userProfiles = await _userService.GetUsersByIdsAsync(uploaderIds);

        // Map folders with non-null uploader fields
        var folders = foldersList.Select(f =>
        {
            var uploader = !string.IsNullOrWhiteSpace(f.UploadedByUserId)
                ? userProfiles.FirstOrDefault(u => u.Id == f.UploadedByUserId)
                : null;

            return new FolderOrFileItem
            {
                Id = f.Id,
                Name = f.Name,
                Type = FolderItemType.Folder,
                CrateId = f.CrateId,
                ParentFolderId = f.ParentFolderId,
                Color = f.Color,

                UploadedByUserId = f.UploadedByUserId ?? string.Empty,
                UploadedByDisplayName = !string.IsNullOrWhiteSpace(f.UploadedByDisplayName)
                    ? f.UploadedByDisplayName!
                    : uploader?.DisplayName ?? "Unknown",
                UploadedByEmail = !string.IsNullOrWhiteSpace(f.UploadedByEmail)
                    ? f.UploadedByEmail!
                    : uploader?.Email ?? string.Empty,
                UploadedByProfilePictureUrl = !string.IsNullOrWhiteSpace(f.UploadedByProfilePictureUrl)
                    ? f.UploadedByProfilePictureUrl!
                    : uploader?.ProfilePictureUrl ?? string.Empty,
                CreatedAt = f.CreatedAt
            };
        }).ToList();

        // Map files (you already had this; kept as-is with safe fallbacks)
        var files = filesList.Select(f =>
        {
            var uploader = userProfiles.FirstOrDefault(u => u.Id == f.UploadedByUserId);
            return new FolderOrFileItem
            {
                Id = f.Id,
                Name = f.Name,
                Type = FolderItemType.File,
                MimeType = f.MimeType,
                SizeInBytes = f.SizeInBytes,
                CrateId = f.CrateId,
                ParentFolderId = f.FolderId,
                Color = null,

                UploadedByUserId = f.UploadedByUserId ?? string.Empty,
                UploadedByDisplayName = uploader?.DisplayName ?? "Unknown",
                UploadedByEmail = uploader?.Email ?? string.Empty,
                UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? string.Empty,
                CreatedAt = f.CreatedAt
            };
        }).ToList();

        var combined = folders.Concat(files)
            .OrderBy(i => i.Type)
            .ThenBy(i => i.Name)
            .ToList();

        var totalCount = combined.Count;
        var pagedItems = combined.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        Guid? parentOfCurrentFolderId = null;
        string currentFolderName = "Root";
        if (parentFolderId.HasValue)
        {
            var folderInfo = await _context.Folders
                .Where(f => f.Id == parentFolderId)
                .Select(f => new { f.Name, f.ParentFolderId })
                .FirstOrDefaultAsync();

            if (folderInfo != null)
            {
                currentFolderName = folderInfo.Name;
                parentOfCurrentFolderId = folderInfo.ParentFolderId;
            }
        }

        return Result<FolderContentsResponse>.Success(new FolderContentsResponse
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            ParentFolderId = parentFolderId,
            ParentOfCurrentFolderId = parentOfCurrentFolderId,
            FolderName = currentFolderName
        });
    }
}