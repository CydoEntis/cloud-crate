using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Mappings;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using FolderEntity = CloudCrate.Domain.Entities.Folder;

namespace CloudCrate.Infrastructure.Services.Folder;

public class FolderService : IFolderService
{
    private readonly IAppDbContext _context;
    private readonly ICratePermissionService _cratePermissionService;
    private readonly IUserService _userService;
    private readonly IFileService _fileService;

    public FolderService(
        IAppDbContext context,
        ICratePermissionService cratePermissionService,
        IUserService userService,
        IFileService fileService)
    {
        _context = context;
        _cratePermissionService = cratePermissionService;
        _userService = userService;
        _fileService = fileService;
    }

    #region Folder Operations

    public async Task<Result<FolderResponse>> CreateFolderAsync(CreateFolderRequest request, string userId)
    {
        var permission = await _cratePermissionService.CheckUploadPermissionAsync(request.CrateId, userId);
        if (!permission.Succeeded) return Result<FolderResponse>.Failure(permission.Errors);

        if (request.ParentFolderId.HasValue)
        {
            bool parentExists = await _context.Folders
                .AnyAsync(f => f.Id == request.ParentFolderId && f.CrateId == request.CrateId);
            if (!parentExists) return Result<FolderResponse>.Failure(Errors.Folders.NotFound);
        }

        var user = await _userService.GetUserByIdAsync(userId);

        var folder = FolderEntity.Create(
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
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId);
        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckUploadPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        folder.Name = newName;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckOwnerPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        if (folder.Subfolders.Any()) return Result.Failure(Errors.Folders.NotEmpty);

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId);
        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckUploadPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        if (newParentId.HasValue)
        {
            var newParent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == newParentId.Value);
            if (newParent == null || newParent.CrateId != folder.CrateId)
                return Result.Failure(Errors.Folders.NotFound);

            if (newParentId == folder.Id) return Result.Failure(Errors.Folders.InvalidMove);

            Guid? current = newParentId;
            while (current != null)
            {
                if (current == folder.Id) return Result.Failure(Errors.Folders.InvalidMove);
                current = await _context.Folders
                    .Where(f => f.Id == current)
                    .Select(f => f.ParentFolderId)
                    .FirstOrDefaultAsync();
            }
        }

        folder.ParentFolderId = newParentId;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    #endregion

    #region Folder Contents

    public async Task<Result<FolderContentsResult>> GetFolderContentsAsync(FolderQueryParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.UserId))
            return Result<FolderContentsResult>.Failure(
                Errors.User.Unauthorized with { Message = "UserId is required" });

        var permission = await _cratePermissionService.CheckViewPermissionAsync(parameters.CrateId, parameters.UserId);
        if (!permission.Succeeded)
            return Result<FolderContentsResult>.Failure(permission.Errors);

        // Fetch crate owner once for root
        var ownerMember = await _context.CrateMembers
            .Where(cm => cm.CrateId == parameters.CrateId && cm.Role == CrateRole.Owner)
            .FirstOrDefaultAsync();

        var ownerUser = ownerMember != null
            ? await _userService.GetUserByIdAsync(ownerMember.UserId)
            : null;

        string rootUserId = ownerUser?.Id ?? string.Empty;
        string rootDisplayName = ownerUser?.DisplayName ?? "Unknown";
        string rootEmail = ownerUser?.Email ?? string.Empty;
        string rootProfilePicture = ownerUser?.ProfilePictureUrl ?? string.Empty;

        List<FolderOrFileItem> folderItems = new();
        List<FileItemDto> fileItemsList;

        if (string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            // --- NORMAL FOLDER VIEW ---
            var folders = await _context.Folders
                .Where(f => f.CrateId == parameters.CrateId && f.ParentFolderId == parameters.ParentFolderId)
                .OrderBy(f => f.Name)
                .ToListAsync();

            var folderUploaderIds = folders
                .Select(f => f.UploadedByUserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var folderUploaders = await _userService.GetUsersByIdsAsync(folderUploaderIds);

            foreach (var f in folders)
            {
                folderItems.Add(await f.ToFolderOrFileItemAsync(folderUploaders, GetFolderSizeRecursiveAsync));
            }

            var fileResult = await _fileService.GetFilesAsync(new()
            {
                CrateId = parameters.CrateId,
                FolderId = parameters.ParentFolderId,
                SearchTerm = null,
                OrderBy = parameters.SortBy == FolderSortBy.CreatedAt ? FileOrderBy.CreatedAt : FileOrderBy.Name,
                Ascending = parameters.OrderBy != OrderBy.Desc,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                UserId = parameters.UserId
            });

            fileItemsList = fileResult.Items ?? new List<FileItemDto>();
        }
        else
        {
            // --- SEARCH MODE ---
            var fileResult = await _fileService.GetFilesAsync(new()
            {
                CrateId = parameters.CrateId,
                FolderId = null,
                SearchTerm = parameters.SearchTerm,
                OrderBy = parameters.SortBy == FolderSortBy.CreatedAt ? FileOrderBy.CreatedAt : FileOrderBy.Name,
                Ascending = parameters.OrderBy != OrderBy.Desc,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                UserId = parameters.UserId
            });

            fileItemsList = fileResult.Items ?? new List<FileItemDto>();

            // Map grandparent folders or root
            var grandParentFolders = new Dictionary<Guid, FolderOrFileItem>();

            foreach (var f in fileItemsList)
            {
                Guid grandParentId = Guid.Empty; // Default to root
                string grandParentName = "Root";
                string uploadedByUserId = rootUserId;
                string uploadedByDisplayName = rootDisplayName;
                string uploadedByEmail = rootEmail;
                string uploadedByProfilePictureUrl = rootProfilePicture;

                if (f.ParentFolderId.HasValue)
                {
                    var parentFolder = await _context.Folders.FirstOrDefaultAsync(x => x.Id == f.ParentFolderId.Value);
                    if (parentFolder != null)
                    {
                        if (parentFolder.ParentFolderId.HasValue)
                        {
                            var grandParent =
                                await _context.Folders.FirstOrDefaultAsync(x =>
                                    x.Id == parentFolder.ParentFolderId.Value);
                            if (grandParent != null)
                            {
                                grandParentId = grandParent.Id;
                                grandParentName = grandParent.Name;
                                uploadedByUserId = grandParent.UploadedByUserId ?? rootUserId;
                                var uploader = !string.IsNullOrWhiteSpace(grandParent.UploadedByUserId)
                                    ? await _userService.GetUserByIdAsync(grandParent.UploadedByUserId)
                                    : null;
                                uploadedByDisplayName = uploader?.DisplayName ?? rootDisplayName;
                                uploadedByEmail = uploader?.Email ?? rootEmail;
                                uploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? rootProfilePicture;
                            }
                        }
                        else
                        {
                            // Parent has no grandparent -> root
                            grandParentId = Guid.Empty;
                            grandParentName = "Root";
                        }
                    }
                }

                if (!grandParentFolders.ContainsKey(grandParentId))
                {
                    grandParentFolders.Add(grandParentId, new FolderOrFileItem
                    {
                        Id = grandParentId,
                        Name = grandParentName,
                        Type = FolderItemType.Folder,
                        CrateId = f.CrateId,
                        ParentFolderId = null,
                        ParentFolderName = null,
                        UploadedByUserId = uploadedByUserId,
                        UploadedByDisplayName = uploadedByDisplayName,
                        UploadedByEmail = uploadedByEmail,
                        UploadedByProfilePictureUrl = uploadedByProfilePictureUrl,
                        CreatedAt = DateTime.UtcNow,
                        SizeInBytes =
                            grandParentId == Guid.Empty ? 0 : await GetFolderSizeRecursiveAsync(grandParentId),
                        MimeType = null,
                        FileUrl = null,
                        Color = null
                    });
                }
            }

            folderItems.AddRange(grandParentFolders.Values);
        }

        // Map file items
        var fileItems = fileItemsList.Select(f => new FolderOrFileItem
        {
            Id = f.Id,
            Name = f.Name,
            Type = FolderItemType.File,
            CrateId = f.CrateId,
            ParentFolderId = f.ParentFolderId,
            ParentFolderName = f.ParentFolderName,
            MimeType = f.MimeType,
            SizeInBytes = f.SizeInBytes,
            UploadedByUserId = f.UploadedByUserId ?? rootUserId,
            UploadedByDisplayName = f.UploadedByDisplayName ?? rootDisplayName,
            UploadedByEmail = f.UploadedByEmail ?? rootEmail,
            UploadedByProfilePictureUrl = f.UploadedByProfilePictureUrl ?? rootProfilePicture,
            CreatedAt = f.CreatedAt,
            FileUrl = f.FileUrl ?? string.Empty
        }).ToList();

        // Combine folders and files
        var items = folderItems.Concat(fileItems).ToList();

        // Determine current folder name
        string folderName = "Root";
        if (parameters.ParentFolderId.HasValue)
        {
            var parentFolder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == parameters.ParentFolderId.Value);
            folderName = parentFolder?.Name ?? "Unknown";
        }

        var result = FolderContentsResult.Create(items, items.Count, parameters.Page, parameters.PageSize);
        result.FolderName = folderName;
        result.ParentFolderId = parameters.ParentFolderId;

        return Result<FolderContentsResult>.Success(result);
    }

    #endregion


    #region Helpers

    private async Task<long> GetFolderSizeRecursiveAsync(Guid folderId)
    {
        long size = await _context.FileObjects
            .Where(f => f.FolderId == folderId)
            .SumAsync(f => (long?)f.SizeInBytes) ?? 0;

        var subIds = await _context.Folders
            .Where(f => f.ParentFolderId == folderId)
            .Select(f => f.Id)
            .ToListAsync();

        foreach (var id in subIds)
            size += await GetFolderSizeRecursiveAsync(id);

        return size;
    }

    #endregion
}