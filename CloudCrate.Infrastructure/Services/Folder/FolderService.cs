using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
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
using CloudCrate.Application.Common.Mappers;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.User;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Infrastructure.Services.Folder;

public class FolderService : IFolderService
{
    private readonly IAppDbContext _context;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IUserService _userService;
    private readonly IFileService _fileService;

    public FolderService(
        IAppDbContext context,
        ICrateRoleService crateRoleService,
        IUserService userService,
        IFileService fileService)
    {
        _context = context;
        _crateRoleService = crateRoleService;
        _userService = userService;
        _fileService = fileService;
    }

    #region Get Folders and Contents

    public async Task<Result<FolderContentsResponse>> GetFolderContentsAsync(FolderContentsParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.UserId))
            return Result<FolderContentsResponse>.Failure(new UnauthorizedError("UserId is required"));

        var permission = await _crateRoleService.CanView(parameters.CrateId, parameters.UserId);
        if (!permission.IsSuccess)
            return Result<FolderContentsResponse>.Failure(permission.Error!);

        bool searchMode = !string.IsNullOrWhiteSpace(parameters.SearchTerm);
        var searchTerm = parameters.SearchTerm?.Trim().ToLower() ?? "";

        var folders = searchMode
            ? await GetFolderItemsAsync(parameters.CrateId, parameters.FolderId, searchTerm)
            : await GetFolderItemsAsync(parameters.CrateId, parameters.FolderId);

        var files = await _fileService.FetchFilesAsync(parameters);

        string folderName = await GetFolderNameAsync(parameters.FolderId);
        var breadcrumbs = await GetFolderBreadcrumbs(parameters.FolderId);

        var response = new FolderContentsResponse
        {
            Folders = folders.ToList(),
            Files = files.Items.ToList(),
            FolderName = folderName,
            ParentFolderId = parameters.FolderId,
            Breadcrumbs = breadcrumbs,
            TotalFolders = folders.Count(),
            TotalFiles = files.TotalCount
        };

        return Result<FolderContentsResponse>.Success(response);
    }

    #endregion

    #region Create Folders

    public async Task<Result<Guid>> CreateFolderAsync(CreateFolderRequest request, string userId)
    {
        var permission = await _crateRoleService.CanUpload(request.CrateId, userId);
        if (!permission.IsSuccess) return Result<Guid>.Failure(permission.Error!);

        if (request.ParentFolderId.HasValue)
        {
            bool parentExists = await _context.CrateFolders
                .AnyAsync(f => f.Id == request.ParentFolderId && f.CrateId == request.CrateId && !f.IsDeleted);
            if (!parentExists) return Result<Guid>.Failure(new NotFoundError("Parent folder not found"));
        }

        var folder = CrateFolder.Create(
            request.Name,
            request.CrateId,
            request.ParentFolderId,
            request.Color,
            userId
        );

        _context.CrateFolders.Add(folder);
        await _context.SaveChangesAsync();

        return Result<Guid>.Success(folder.Id);
    }

    #endregion

    #region Update Folders

    public async Task<Result> UpdateFolderAsync(Guid folderId, UpdateFolderRequest request, string userId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
        if (folder == null) return Result.Failure(new NotFoundError("Folder not found"));

        var permission = await _crateRoleService.CanUpload(folder.CrateId, userId);
        if (!permission.IsSuccess) return Result.Failure(permission.Error!);

        folder.Name = request.NewName ?? folder.Name;
        folder.Color = request.NewColor ?? folder.Color;
        folder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    #endregion

    #region Delete Folders

    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);

        if (folder == null) return Result.Failure(new NotFoundError("Folder not found"));

        var permission = await _crateRoleService.CanManageCrate(folder.CrateId, userId);
        if (!permission.IsSuccess) return Result.Failure(permission.Error!);

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
            return Result.Failure(new NotFoundError("Folder not found"));

        var permission = await _crateRoleService.CanManageCrate(folder.CrateId, userId);
        if (!permission.IsSuccess) return Result.Failure(permission.Error!);

        return await SoftDeleteFolderRecursiveAsync(folder, userId);
    }

    public async Task<Result> PermanentlyDeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null) return Result.Failure(new NotFoundError("Folder not found"));

        var permission = await _crateRoleService.CanManageCrate(folder.CrateId, userId);
        if (!permission.IsSuccess) return Result.Failure(permission.Error!);

        var fileIds = (await _fileService.FetchFilesInFolderRecursivelyAsync(folderId))
            .Select(f => f.Id).ToList();

        if (fileIds.Any())
        {
            var fileResult = await _fileService.PermanentlyDeleteFilesAsync(fileIds, userId);
            if (!fileResult.IsSuccess) return Result.Failure(fileResult.Error!);
        }

        foreach (var subfolder in folder.Subfolders)
        {
            var subResult = await PermanentlyDeleteFolderAsync(subfolder.Id, userId);
            if (!subResult.IsSuccess) return Result.Failure(subResult.Error!);
        }

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteMultipleAsync(MultipleDeleteRequest request, string userId)
    {
        if (request.FileIds.Any())
        {
            var fileResult = request.Permanent
                ? await _fileService.PermanentlyDeleteFilesAsync(request.FileIds, userId)
                : await _fileService.SoftDeleteFilesAsync(request.FileIds, userId);

            if (!fileResult.IsSuccess) return Result.Failure(fileResult.Error!);
        }

        foreach (var folderId in request.FolderIds)
        {
            Result folderResult;
            if (request.Permanent)
            {
                folderResult = await PermanentlyDeleteFolderAsync(folderId, userId);
            }
            else
            {
                var folder = await _context.Folders
                    .Include(f => f.Subfolders)
                    .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
                if (folder == null) return Result.Failure(new NotFoundError("Folder not found"));

                var permission = await _crateRoleService.CanManageCrate(folder.CrateId, userId);
                if (!permission.IsSuccess) return Result.Failure(permission.Error!);

                folderResult = await SoftDeleteFolderRecursiveAsync(folder, userId);
            }

            if (!folderResult.IsSuccess) return Result.Failure(folderResult.Error!);
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<Result> SoftDeleteFolderRecursiveAsync(FolderEntity folder, string userId)
    {
        folder.IsDeleted = true;
        folder.UpdatedAt = DateTime.UtcNow;

        var files = await _fileService.FetchFilesInFolderRecursivelyAsync(folder.Id);
        if (files.Any())
        {
            var fileResult = await _fileService.SoftDeleteFilesAsync(files.Select(f => f.Id).ToList(), userId);
            if (!fileResult.IsSuccess) return Result.Failure(fileResult.Error!);
        }

        foreach (var sub in folder.Subfolders)
        {
            var subResult = await SoftDeleteFolderRecursiveAsync(sub, userId);
            if (!subResult.IsSuccess) return Result.Failure(subResult.Error!);
        }

        return Result.Success();
    }

    #endregion

    #region Move Folders

    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
        if (folder == null) return Result.Failure(new NotFoundError("Folder not found"));

        var permission = await _crateRoleService.CanUpload(folder.CrateId, userId);
        if (!permission.IsSuccess) return Result.Failure(permission.Error!);

        if (newParentId.HasValue)
        {
            var newParent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == newParentId.Value && !f.IsDeleted);
            if (newParent == null || newParent.CrateId != folder.CrateId)
                return Result.Failure(new NotFoundError("Target parent folder not found"));

            if (newParentId == folder.Id) return Result.Failure(new AlreadyExistsError("Cannot move folder into itself"));

            Guid? current = newParentId;
            while (current != null)
            {
                if (current == folder.Id) return Result.Failure(new AlreadyExistsError("Cannot move folder into a descendant"));
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

    public async Task<Result<List<FolderResponse>>> GetAvailableMoveFoldersAsync(
        Guid crateId,
        Guid? excludeFolderId
    )
    {
        var allFolders = await _context.Folders
            .Where(f => f.CrateId == crateId && !f.IsDeleted)
            .ToListAsync();

        if (excludeFolderId.HasValue)
        {
            var excludedIds = GetDescendantFolderIds(allFolders, excludeFolderId.Value);
            excludedIds.Add(excludeFolderId.Value);

            allFolders = allFolders
                .Where(f => !excludedIds.Contains(f.Id))
                .ToList();
        }

        var response = allFolders.Select(f => new FolderResponse
        {
            Id = f.Id,
            Name = f.Name,
            CrateId = f.CrateId,
            ParentFolderId = f.ParentFolderId,
            Color = f.Color,
            UploadedByUserId = f.UploadedByUserId ?? string.Empty,
            UploadedByDisplayName = f.UploadedByDisplayName ?? string.Empty,
            UploadedByEmail = f.UploadedByEmail ?? string.Empty,
            UploadedByProfilePictureUrl = f.UploadedByProfilePictureUrl ?? string.Empty,
            CreatedAt = f.CreatedAt
        }).ToList();

        return Result<List<FolderResponse>>.Success(response);
    }

    #endregion

    #region Download Folders

    public async Task<Result<FolderDownloadResult>> DownloadFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
        if (folder == null) return Result<FolderDownloadResult>.Failure(new NotFoundError("Folder not found"));

        var permission = await _crateRoleService.CanDownload(folder.CrateId, userId);
        if (!permission.IsSuccess) return Result<FolderDownloadResult>.Failure(permission.Error!);

        var allFolders = await _context.Folders
            .Where(f => f.CrateId == folder.CrateId && !f.IsDeleted)
            .ToListAsync();

        var allFiles = await _fileService.FetchFilesInFolderRecursivelyAsync(folderId);

        using var memoryStream = new MemoryStream();
        using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            await AddFolderToZipOptimizedAsync(folder, allFolders, allFiles, zip, userId, string.Empty);
        }

        memoryStream.Position = 0;

        return Result<FolderDownloadResult>.Success(new FolderDownloadResult
        {
            FileBytes = memoryStream.ToArray(),
            FileName = folder.Name
        });
    }

    #endregion

    #region Restore Folders

    public async Task<Result> RestoreFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Subfolders)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.IsDeleted);

        if (folder == null) return Result.Failure(new NotFoundError("Folder not found"));

        var permission = await _crateRoleService.CanManageCrate(folder.CrateId, userId);
        if (!permission.IsSuccess) return Result.Failure(permission.Error!);

        if (folder.ParentFolderId.HasValue)
        {
            var current = folder.ParentFolderId;
            while (current != null)
            {
                var parent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == current);
                if (parent == null) return Result.Failure(new NotFoundError("Parent folder not found"));
                if (parent.IsDeleted)
                    return Result.Failure(new AlreadyExistsError(
                        "A parent folder is deleted. Restore parent(s) first or move to root."));
                current = parent.ParentFolderId;
            }
        }

        folder.IsDeleted = false;
        folder.UpdatedAt = DateTime.UtcNow;

        var files = await _fileService.FetchFilesInFolderRecursivelyAsync(folder.Id);
        if (files.Any())
            await _fileService.RestoreFilesAsync(files.Select(f => f.Id).ToList(), userId);

        foreach (var sub in folder.Subfolders)
            await RestoreFolderAsync(sub.Id, userId);

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    #endregion

    #region Private Helpers

    private List<Guid> GetDescendantFolderIds(List<FolderEntity> allFolders, Guid parentId)
    {
        var children = allFolders.Where(f => f.ParentFolderId == parentId).ToList();
        var result = new List<Guid>();

        foreach (var child in children)
        {
            result.Add(child.Id);
            result.AddRange(GetDescendantFolderIds(allFolders, child.Id));
        }

        return result;
    }

    private async Task AddFolderToZipOptimizedAsync(
        FolderEntity folder,
        List<FolderEntity> allFolders,
        List<CrateFile> allFiles,
        ZipArchive zip,
        string userId,
        string currentPath)
    {
        string folderPath = string.IsNullOrEmpty(currentPath) ? folder.Name : Path.Combine(currentPath, folder.Name);

        var filesInThisFolder = allFiles.Where(f => f.CrateFolderId == folder.Id && !f.IsDeleted);

        foreach (var file in filesInThisFolder)
        {
            var fileBytesResult = await _fileService.FetchFileBytesAsync(file.Id, userId);
            if (!fileBytesResult.IsSuccess) continue;

            string filePathInZip = Path.Combine(folderPath, file.Name).Replace("\\", "/");
            var zipEntry = zip.CreateEntry(filePathInZip, CompressionLevel.Fastest);

            using var entryStream = zipEntry.Open();
            using var fileStream = new MemoryStream(fileBytesResult.Value);
            await fileStream.CopyToAsync(entryStream);
        }

        var subfolders = allFolders.Where(f => f.ParentFolderId == folder.Id);
        foreach (var sub in subfolders)
            await AddFolderToZipOptimizedAsync(sub, allFolders, allFiles, zip, userId, folderPath);
    }

    private async Task<List<CrateFolderResponse>> GetFolderItemsAsync(
        Guid crateId,
        Guid? parentFolderId,
        string? searchTerm = null)
    {
        var query = _context.Folders
            .Where(f => f.CrateId == crateId && f.ParentFolderId == parentFolderId && !f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{searchTerm}%"));

        var folders = await query.OrderBy(f => f.Name).ToListAsync();

        var uploaderIds = folders
            .Select(f => f.UploadedByUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var uploaders = await _userService.GetUsersByIdsAsync(uploaderIds);

        var result = new List<CrateFolderResponse>();

        foreach (var folder in folders)
        {
            var uploader = uploaders.FirstOrDefault(u => u.Id == folder.UploadedByUserId);

            result.Add(new CrateFolderResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                Color = folder.Color ?? "#EAAC00",
                ParentFolderId = folder.ParentFolderId,
                ParentFolderName = folder.ParentFolder?.Name,
                CrateId = folder.CrateId,
                CreatedAt = folder.CreatedAt,
                UpdatedAt = folder.UpdatedAt,
                IsDeleted = folder.IsDeleted
            });
        }

        return result;
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
                Name = folder.Name
            });

            currentId = folder.ParentFolderId;
        }

        return breadcrumbs;
    }

    #endregion
}
