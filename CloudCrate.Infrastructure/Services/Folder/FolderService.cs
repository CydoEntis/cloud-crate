using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.User.Response;
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

    public FolderService(IAppDbContext context, ICratePermissionService cratePermissionService,
        IUserService userService)
    {
        _context = context;
        _cratePermissionService = cratePermissionService;
        _userService = userService;
    }

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
            .Include(f => f.Files)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckOwnerPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        if (folder.Subfolders.Any() || folder.Files.Any()) return Result.Failure(Errors.Folders.NotEmpty);

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

    public async Task<Result<FolderContentsResponse>> GetFolderContentsAsync(
        Guid crateId, Guid? parentFolderId, string userId, string? search, int page = 1, int pageSize = 20)
    {
        var permission = await _cratePermissionService.CheckViewPermissionAsync(crateId, userId);
        if (!permission.Succeeded) return Result<FolderContentsResponse>.Failure(permission.Errors);

        var (foldersList, filesList) = await LoadFoldersAndFilesAsync(crateId, parentFolderId, search);

        var uploaderIds = foldersList.Select(f => f.UploadedByUserId)
            .Concat(filesList.Select(f => f.UploadedByUserId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var users = await _userService.GetUsersByIdsAsync(uploaderIds);

        var folderItems = new List<FolderOrFileItem>();
        foreach (var f in foldersList)
            folderItems.Add(await MapFolderToItemRecursiveAsync(f, users));

        var fileItems = filesList.Select(f => MapFileToItem(f, users)).ToList();

        var allItems = folderItems.Concat(fileItems)
            .OrderBy(i => i.Type)
            .ThenBy(i => i.Name)
            .ToList();

        int totalCount = allItems.Count;
        var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var (currentFolderName, parentOfCurrentFolderId) = await GetParentFolderInfoAsync(parentFolderId);

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

    #region Helpers

    private async Task<(List<FolderEntity>, List<FileObject>)> LoadFoldersAndFilesAsync(Guid crateId,
        Guid? parentFolderId, string? search)
    {
        var foldersQuery = _context.Folders.Where(f => f.CrateId == crateId && f.ParentFolderId == parentFolderId);
        var filesQuery = _context.FileObjects.Where(f => f.CrateId == crateId && f.FolderId == parentFolderId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            foldersQuery = foldersQuery.Where(f => EF.Functions.ILike(f.Name, $"%{search}%"));
            filesQuery = filesQuery.Where(f => EF.Functions.ILike(f.Name, $"%{search}%"));
        }

        return (await foldersQuery.ToListAsync(), await filesQuery.ToListAsync());
    }

    private async Task<FolderOrFileItem> MapFolderToItemRecursiveAsync(FolderEntity folder, List<UserResponse> users)
    {
        var uploader = !string.IsNullOrWhiteSpace(folder.UploadedByUserId)
            ? users.FirstOrDefault(u => u.Id == folder.UploadedByUserId)
            : null;

        long size = await GetFolderSizeRecursiveAsync(folder.Id);

        return new FolderOrFileItem
        {
            Id = folder.Id,
            Name = folder.Name,
            Type = FolderItemType.Folder,
            CrateId = folder.CrateId,
            ParentFolderId = folder.ParentFolderId,
            Color = folder.Color,
            SizeInBytes = size,
            UploadedByUserId = folder.UploadedByUserId ?? string.Empty,
            UploadedByDisplayName = folder.UploadedByDisplayName ?? uploader?.DisplayName ?? "Unknown",
            UploadedByEmail = folder.UploadedByEmail ?? uploader?.Email ?? string.Empty,
            UploadedByProfilePictureUrl =
                folder.UploadedByProfilePictureUrl ?? uploader?.ProfilePictureUrl ?? string.Empty,
            CreatedAt = folder.CreatedAt
        };
    }

    private async Task<long> GetFolderSizeRecursiveAsync(Guid folderId)
    {
        long size = await _context.FileObjects
            .Where(f => f.FolderId == folderId)
            .SumAsync(f => (long?)f.SizeInBytes) ?? 0;

        var subIds = await _context.Folders.Where(f => f.ParentFolderId == folderId).Select(f => f.Id).ToListAsync();
        foreach (var id in subIds) size += await GetFolderSizeRecursiveAsync(id);

        return size;
    }

    private FolderOrFileItem MapFileToItem(FileObject file, List<UserResponse> users)
    {
        var uploader = users.FirstOrDefault(u => u.Id == file.UploadedByUserId);

        return new FolderOrFileItem
        {
            Id = file.Id,
            Name = file.Name,
            Type = FolderItemType.File,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            ParentFolderId = file.FolderId,
            Color = null,
            UploadedByUserId = file.UploadedByUserId ?? string.Empty,
            UploadedByDisplayName = uploader?.DisplayName ?? "Unknown",
            UploadedByEmail = uploader?.Email ?? string.Empty,
            UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? string.Empty,
            CreatedAt = file.CreatedAt
        };
    }

    private async Task<(string folderName, Guid? parentFolderId)> GetParentFolderInfoAsync(Guid? parentFolderId)
    {
        if (!parentFolderId.HasValue) return ("Root", null);

        var folder = await _context.Folders
            .Where(f => f.Id == parentFolderId)
            .Select(f => new { f.Name, f.ParentFolderId })
            .FirstOrDefaultAsync();

        return folder == null ? ("Root", null) : (folder.Name, folder.ParentFolderId);
    }

    #endregion
}