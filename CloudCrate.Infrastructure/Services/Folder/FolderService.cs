using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Mappings;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using FolderEntity = CloudCrate.Domain.Entities.Folder;
using System.IO.Compression;
using CloudCrate.Application.DTOs;

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
                .AnyAsync(f => f.Id == request.ParentFolderId && f.CrateId == request.CrateId && !f.IsDeleted);
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

        return Result<FolderResponse>.Success(FolderItemMapper.MapFolderResponse(folder));
    }

    public async Task<Result> UpdateFolderAsync(Guid folderId, UpdateFolderRequest request, string userId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckUploadPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        folder.Name = request.NewName ?? folder.Name;
        folder.Color = request.NewColor ?? folder.Color;
        folder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);

        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckOwnerPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        await SoftDeleteFolderRecursiveAsync(folder, userId);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> SoftDeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .Include(f => f.Files)
            .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);

        if (folder == null)
            return Result.Failure(Errors.Folders.NotFound);

        return await SoftDeleteFolderRecursiveAsync(folder, userId);
    }

    public async Task<Result> PermanentlyDeleteFolderAsync(Guid folderId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var fileIds = (await _fileService.GetFilesInFolderRecursivelyAsync(folderId))
            .Select(f => f.Id).ToList();

        if (fileIds.Any())
        {
            var fileResult = await _fileService.PermanentlyDeleteFilesAsync(fileIds, "");
            if (!fileResult.Succeeded) return fileResult;
        }

        foreach (var subfolder in folder.Subfolders)
        {
            var subResult = await PermanentlyDeleteFolderAsync(subfolder.Id);
            if (!subResult.Succeeded) return subResult;
        }

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteMultipleAsync(MultipleDeleteRequest request, string userId)
    {
        if (request.FileIds.Any())
        {
            Result fileResult = request.Permanent
                ? await _fileService.PermanentlyDeleteFilesAsync(request.FileIds, userId)
                : await _fileService.SoftDeleteFilesAsync(request.FileIds, userId);

            if (!fileResult.Succeeded) return fileResult;
        }

        foreach (var folderId in request.FolderIds)
        {
            Result folderResult;
            if (request.Permanent)
            {
                folderResult = await PermanentlyDeleteFolderAsync(folderId);
            }
            else
            {
                var folder = await _context.Folders
                    .Include(f => f.Subfolders)
                    .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
                if (folder == null) return Result.Failure(Errors.Folders.NotFound);

                folderResult = await SoftDeleteFolderRecursiveAsync(folder, userId);
            }

            if (!folderResult.Succeeded) return folderResult;
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<Result> SoftDeleteFolderRecursiveAsync(FolderEntity folder, string userId)
    {
        folder.IsDeleted = true;
        folder.UpdatedAt = DateTime.UtcNow;

        var files = await _fileService.GetFilesInFolderRecursivelyAsync(folder.Id);
        if (files.Any())
        {
            var fileResult = await _fileService.SoftDeleteFilesAsync(files.Select(f => f.Id).ToList(), userId);
            if (!fileResult.Succeeded) return fileResult;
        }

        foreach (var sub in folder.Subfolders)
        {
            var subResult = await SoftDeleteFolderRecursiveAsync(sub, userId);
            if (!subResult.Succeeded) return subResult;
        }

        return Result.Success();
    }

    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckUploadPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        if (newParentId.HasValue)
        {
            var newParent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == newParentId.Value && !f.IsDeleted);
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
        folder.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    #endregion

    #region Folder Downloads

    public async Task<Result<FolderDownloadResult>> DownloadFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
        if (folder == null) return Result<FolderDownloadResult>.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckViewPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result<FolderDownloadResult>.Failure(permission.Errors);

        using var memoryStream = new MemoryStream();
        using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            await AddFolderToZipAsync(folder, zip, userId, string.Empty);
        }

        memoryStream.Position = 0;

        return Result<FolderDownloadResult>.Success(new FolderDownloadResult
        {
            FileBytes = memoryStream.ToArray(),
            FileName = folder.Name
        });
    }

    private async Task AddFolderToZipAsync(FolderEntity folder, ZipArchive zip, string userId, string currentPath)
    {
        string folderPath = string.IsNullOrEmpty(currentPath) ? folder.Name : Path.Combine(currentPath, folder.Name);

        var files = await _fileService.GetFilesInFolderRecursivelyAsync(folder.Id);
        foreach (var file in files)
        {
            var fileBytesResult = await _fileService.GetFileBytesAsync(file.Id, userId);
            if (!fileBytesResult.Succeeded) continue;

            string filePathInZip = Path.Combine(folderPath, file.Name).Replace("\\", "/");
            var zipEntry = zip.CreateEntry(filePathInZip, CompressionLevel.Fastest);

            using var entryStream = zipEntry.Open();
            using var fileStream = new MemoryStream(fileBytesResult.Value);
            await fileStream.CopyToAsync(entryStream);
        }

        var subfolders = await _context.Folders
            .Where(f => f.ParentFolderId == folder.Id && !f.IsDeleted)
            .ToListAsync();

        foreach (var sub in subfolders)
            await AddFolderToZipAsync(sub, zip, userId, folderPath);
    }

    #endregion

    #region Folder Contents & Helpers

    public async Task<Result<FolderContentsResult>> GetFolderContentsAsync(FolderQueryParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.UserId))
            return Result<FolderContentsResult>.Failure(
                Errors.User.Unauthorized with { Message = "UserId is required" });

        var permission = await _cratePermissionService.CheckViewPermissionAsync(parameters.CrateId, parameters.UserId);
        if (!permission.Succeeded)
            return Result<FolderContentsResult>.Failure(permission.Errors);

        bool searchMode = !string.IsNullOrWhiteSpace(parameters.SearchTerm);
        var searchTerm = parameters.SearchTerm?.Trim().ToLower() ?? "";

        var folderItems = searchMode
            ? await GetFolderItemsAsync(parameters.CrateId, parameters.ParentFolderId, searchTerm)
            : await GetFolderItemsAsync(parameters.CrateId, parameters.ParentFolderId);

        var fileItems = await _fileService.GetFilesForFolderContentsAsync(parameters, searchMode, searchTerm);

        var allItems = SortAndCombineItems(folderItems, fileItems, parameters.SortBy, parameters.OrderBy);

        var paginated = allItems.Paginate(parameters.Page, parameters.PageSize);

        string folderName = await GetFolderNameAsync(parameters.ParentFolderId);
        var breadcrumbs = await GetFolderBreadcrumbs(parameters.ParentFolderId);

        var result = FolderContentsResult.Create(
            items: paginated.Items.ToList(),
            totalCount: paginated.TotalCount,
            page: paginated.Page,
            pageSize: paginated.PageSize,
            folderName: folderName,
            parentFolderId: parameters.ParentFolderId,
            breadcrumbs: breadcrumbs
        );

        return Result<FolderContentsResult>.Success(result);
    }

    #endregion

    #region Restore Folder

    public async Task<Result> RestoreFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.IsDeleted);

        if (folder == null) return Result.Failure(Errors.Folders.NotFound);

        var permission = await _cratePermissionService.CheckUploadPermissionAsync(folder.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        if (folder.ParentFolderId.HasValue)
        {
            var current = folder.ParentFolderId;
            while (current != null)
            {
                var parent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == current);
                if (parent == null) return Result.Failure(Errors.Folders.NotFound);
                if (parent.IsDeleted)
                    return Result.Failure(Errors.Folders.InvalidMove with
                    {
                        Message = "A parent folder is deleted. Restore parent(s) first or move to root."
                    });
                current = parent.ParentFolderId;
            }
        }

        folder.IsDeleted = false;
        folder.UpdatedAt = DateTime.UtcNow;

        var files = await _fileService.GetFilesInFolderRecursivelyAsync(folder.Id);
        if (files.Any())
            await _fileService.RestoreFilesAsync(files.Select(f => f.Id).ToList(), userId);

        foreach (var sub in folder.Subfolders)
            await RestoreFolderAsync(sub.Id, userId);

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    #endregion

    #region Private Helpers

    private async Task<List<FolderOrFileItem>> GetFolderItemsAsync(Guid crateId, Guid? parentFolderId,
        string? searchTerm = null)
    {
        var query = _context.Folders
            .Where(f => f.CrateId == crateId && f.ParentFolderId == parentFolderId && !f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(f => f.Name.ToLower().Contains(searchTerm));

        var folders = await query.OrderBy(f => f.Name).ToListAsync();

        var uploaderIds = folders
            .Select(f => f.UploadedByUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var uploaders = await _userService.GetUsersByIdsAsync(uploaderIds);

        var result = new List<FolderOrFileItem>();
        foreach (var folder in folders)
            result.Add(await FolderItemMapper.MapFolderAsync(folder, uploaders,
                _fileService.GetFolderFilesSizeRecursiveAsync));

        return result;
    }

    private static IEnumerable<FolderOrFileItem> SortAndCombineItems(
        IEnumerable<FolderOrFileItem> folders,
        IEnumerable<FolderOrFileItem> files,
        FolderSortBy sortBy,
        OrderBy orderBy)
    {
        folders = SortItems(folders, sortBy, orderBy);
        files = SortItems(files, sortBy, orderBy);
        return folders.Concat(files);
    }

    private static IOrderedEnumerable<FolderOrFileItem> SortItems(
        IEnumerable<FolderOrFileItem> items,
        FolderSortBy sortBy,
        OrderBy orderBy)
    {
        return sortBy switch
        {
            FolderSortBy.Name => orderBy == OrderBy.Asc
                ? items.OrderBy(i => i.Name)
                : items.OrderByDescending(i => i.Name),
            FolderSortBy.CreatedAt => orderBy == OrderBy.Asc
                ? items.OrderBy(i => i.CreatedAt)
                : items.OrderByDescending(i => i.CreatedAt),
            FolderSortBy.Size => orderBy == OrderBy.Asc
                ? items.OrderBy(i => i.SizeInBytes)
                : items.OrderByDescending(i => i.SizeInBytes),
            _ => items.OrderBy(i => i.Name)
        };
    }

    private async Task<string> GetFolderNameAsync(Guid? folderId)
    {
        if (!folderId.HasValue) return "Root";
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId.Value && !f.IsDeleted);
        return folder?.Name ?? "Unknown";
    }

    private async Task<List<FolderBreadcrumb>> GetFolderBreadcrumbs(Guid? folderId)
    {
        if (!folderId.HasValue) return new List<FolderBreadcrumb>();

        var allFolders = await _context.Folders.Where(f => !f.IsDeleted).ToListAsync();
        var breadcrumbs = new List<FolderBreadcrumb>();
        var currentId = folderId;

        while (currentId.HasValue)
        {
            var folder = allFolders.FirstOrDefault(f => f.Id == currentId.Value);
            if (folder == null) break;

            breadcrumbs.Insert(0, new FolderBreadcrumb
            {
                Id = folder.Id,
                Name = folder.Name,
                Color = folder.Color ?? "#9CA3AF"
            });

            currentId = folder.ParentFolderId;
        }

        return breadcrumbs;
    }

    #endregion
}