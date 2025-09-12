using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.User;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Folder;

public class FolderService : IFolderService
{
    private readonly AppDbContext _context;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IUserService _userService;
    private readonly IFileService _fileService;
    private readonly IStorageService _storageService;
    private readonly ILogger<FolderService> _logger;

    public FolderService(
        AppDbContext context,
        ICrateRoleService crateRoleService,
        IUserService userService,
        IFileService fileService,
        IStorageService storageService,
        ILogger<FolderService> logger)
    {
        _context = context;
        _crateRoleService = crateRoleService;
        _userService = userService;
        _fileService = fileService;
        _storageService = storageService;
        _logger = logger;
    }

    #region Get Folders and Contents

    public async Task<Result<FolderContentsResponse>> GetFolderContentsAsync(FolderContentsParameters parameters)
    {
        try
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

            if (parameters.FolderId.HasValue)
            {
                var currentFolder = await _context.CrateFolders
                    .Include(f => f.ParentFolder)
                    .FirstOrDefaultAsync(f => f.Id == parameters.FolderId.Value && !f.IsDeleted);

                if (currentFolder?.ParentFolder != null)
                {
                    var parentFolder = new CrateFolderResponse
                    {
                        Id = currentFolder.ParentFolder.Id,
                        Name = currentFolder.ParentFolder.Name,
                        Color = currentFolder.ParentFolder.Color,
                        ParentFolderId = currentFolder.ParentFolder.ParentFolderId,
                        CreatedAt = currentFolder.ParentFolder.CreatedAt,
                        UpdatedAt = currentFolder.ParentFolder.UpdatedAt
                    };

                    folders.Insert(0, parentFolder);
                }
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetFolderContentsAsync for CrateId {CrateId}, UserId {UserId}",
                parameters.CrateId, parameters.UserId);
            return Result<FolderContentsResponse>.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Create Folders

    public async Task<Result<Guid>> CreateFolderAsync(CreateFolderRequest request, string userId)
    {
        try
        {
            var permission = await _crateRoleService.CanUpload(request.CrateId, userId);
            if (!permission.IsSuccess) return Result<Guid>.Failure(permission.Error!);

            if (request.ParentFolderId.HasValue)
            {
                bool parentExists = await _context.CrateFolders
                    .AnyAsync(f => f.Id == request.ParentFolderId && f.CrateId == request.CrateId && !f.IsDeleted);
                if (!parentExists) return Result<Guid>.Failure(new NotFoundError("Parent folder not found"));
            }

            var domainFolder = CrateFolder.Create(
                request.Name,
                request.CrateId,
                request.ParentFolderId,
                request.Color,
                userId
            );

            var folderEntity = domainFolder.ToEntity(request.CrateId);

            _context.CrateFolders.Add(folderEntity);
            await _context.SaveChangesAsync();

            return Result<Guid>.Success(folderEntity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CreateFolderAsync for CrateId {CrateId}, UserId {UserId}",
                request.CrateId, userId);
            return Result<Guid>.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Update Folders

    public async Task<Result> UpdateFolderAsync(Guid folderId, UpdateFolderRequest request, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
            if (folderEntity == null) return Result.Failure(new NotFoundError("Folder not found"));

            var permission = await _crateRoleService.CanUpload(folderEntity.CrateId, userId);
            if (!permission.IsSuccess) return Result.Failure(permission.Error!);

            folderEntity.Name = request.NewName ?? folderEntity.Name;
            folderEntity.Color = request.NewColor ?? folderEntity.Color;
            folderEntity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Delete Folders

    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders
                .Include(f => f.Subfolders)
                .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);

            if (folderEntity == null) return Result.Failure(new NotFoundError("Folder not found"));

            var permission = await _crateRoleService.CanManageCrate(folderEntity.CrateId, userId);
            if (!permission.IsSuccess) return Result.Failure(permission.Error!);

            await SoftDeleteFolderRecursiveAsync(folderEntity, userId);
            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeleteFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> SoftDeleteFolderAsync(Guid folderId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders
                .Include(f => f.Subfolders)
                .Include(f => f.Files)
                .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);

            if (folderEntity == null)
                return Result.Failure(new NotFoundError("Folder not found"));

            var permission = await _crateRoleService.CanManageCrate(folderEntity.CrateId, userId);
            if (!permission.IsSuccess) return Result.Failure(permission.Error!);

            return await SoftDeleteFolderRecursiveAsync(folderEntity, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in SoftDeleteFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> PermanentlyDeleteFolderAsync(Guid folderId, string userId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var rootFolder = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId);
            if (rootFolder == null)
                return Result.Failure(new NotFoundError("Folder not found"));

            var permission = await _crateRoleService.CanManageCrate(rootFolder.CrateId, userId);
            if (!permission.IsSuccess)
                return Result.Failure(permission.Error!);

            var allFolders = await _context.CrateFolders
                .Where(f => f.CrateId == rootFolder.CrateId)
                .ToListAsync();

            var folderIdsToDelete = GetDescendantFolderIds(allFolders, folderId);
            folderIdsToDelete.Add(folderId);

            var allFiles = await _context.CrateFiles
                .Where(f => f.CrateFolderId != null && folderIdsToDelete.Contains(f.CrateFolderId.Value))
                .ToListAsync();

            foreach (var file in allFiles)
            {
                var storageResult =
                    await _storageService.DeleteFileAsync(file.CrateId, file.CrateFolderId, file.Name);
                if (storageResult.IsFailure) return Result.Failure(storageResult.Error!);

                var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == file.CrateId);
                if (crateEntity != null)
                {
                    var crate = crateEntity.ToDomain();
                    crate.ReleaseStorage(StorageSize.FromBytes(file.SizeInBytes));
                    crateEntity.UpdateEntity(crate);
                }

                var userStorageResult =
                    await _userService.DecrementUsedStorageAsync(file.UploadedByUserId, file.SizeInBytes);
                if (userStorageResult.IsFailure) return Result.Failure(userStorageResult.Error!);
            }

            _context.CrateFiles.RemoveRange(allFiles);

            var foldersToDelete = allFolders
                .Where(f => folderIdsToDelete.Contains(f.Id))
                .OrderByDescending(f => GetFolderDepth(f, allFolders)) // leaf first
                .ToList();

            _context.CrateFolders.RemoveRange(foldersToDelete);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in PermanentlyDeleteFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            await transaction.RollbackAsync();
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    private int GetFolderDepth(CrateFolderEntity folder, List<CrateFolderEntity> allFolders)
    {
        int depth = 0;
        var current = folder;
        while (current.ParentFolderId.HasValue)
        {
            depth++;
            current = allFolders.FirstOrDefault(f => f.Id == current.ParentFolderId.Value);
            if (current == null) break;
        }

        return depth;
    }

    public async Task<Result> DeleteMultipleAsync(MultipleDeleteRequest request, string userId)
    {
        try
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
                    var folderEntity = await _context.CrateFolders
                        .Include(f => f.Subfolders)
                        .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
                    if (folderEntity == null) return Result.Failure(new NotFoundError("Folder not found"));

                    var permission = await _crateRoleService.CanManageCrate(folderEntity.CrateId, userId);
                    if (!permission.IsSuccess) return Result.Failure(permission.Error!);

                    folderResult = await SoftDeleteFolderRecursiveAsync(folderEntity, userId);
                }

                if (!folderResult.IsSuccess) return Result.Failure(folderResult.Error!);
            }

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeleteMultipleAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    private async Task<Result> SoftDeleteFolderRecursiveAsync(CrateFolderEntity folderEntity, string userId)
    {
        try
        {
            folderEntity.IsDeleted = true;
            folderEntity.DeletedAt = DateTime.UtcNow;
            folderEntity.DeletedByUserId = userId;
            folderEntity.UpdatedAt = DateTime.UtcNow;

            var files = await _fileService.FetchFilesInFolderRecursivelyAsync(folderEntity.Id);
            if (files.Any())
            {
                var fileResult = await _fileService.SoftDeleteFilesAsync(files.Select(f => f.Id).ToList(), userId);
                if (!fileResult.IsSuccess) return Result.Failure(fileResult.Error!);
            }

            foreach (var sub in folderEntity.Subfolders)
            {
                var subResult = await SoftDeleteFolderRecursiveAsync(sub, userId);
                if (!subResult.IsSuccess) return Result.Failure(subResult.Error!);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in SoftDeleteFolderRecursiveAsync for FolderId {FolderId}, UserId {UserId}",
                folderEntity.Id, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Move Folders

    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
            if (folderEntity == null) return Result.Failure(new NotFoundError("Folder not found"));

            var permission = await _crateRoleService.CanUpload(folderEntity.CrateId, userId);
            if (!permission.IsSuccess) return Result.Failure(permission.Error!);

            if (newParentId.HasValue)
            {
                var newParent =
                    await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == newParentId.Value && !f.IsDeleted);
                if (newParent == null || newParent.CrateId != folderEntity.CrateId)
                    return Result.Failure(new NotFoundError("Target parent folder not found"));

                if (newParentId == folderEntity.Id)
                    return Result.Failure(new AlreadyExistsError("Cannot move folder into itself"));

                Guid? current = newParentId;
                while (current != null)
                {
                    if (current == folderEntity.Id)
                        return Result.Failure(new AlreadyExistsError("Cannot move folder into a descendant"));
                    current = await _context.CrateFolders
                        .Where(f => f.Id == current)
                        .Select(f => f.ParentFolderId)
                        .FirstOrDefaultAsync();
                }
            }

            var storageResult =
                await _storageService.MoveFolderAsync(folderEntity.CrateId, folderEntity.Id, newParentId);
            if (!storageResult.IsSuccess) return storageResult;

            folderEntity.ParentFolderId = newParentId;
            folderEntity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in MoveFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<List<FolderResponse>>> GetAvailableMoveFoldersAsync(
        Guid crateId,
        Guid? excludeFolderId
    )
    {
        try
        {
            var allFolders = await _context.CrateFolders
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

            var userIds = allFolders
                .Where(f => !string.IsNullOrEmpty(f.CreatedByUserId))
                .Select(f => f.CreatedByUserId!)
                .Distinct()
                .ToList();

            var users = await _userService.GetUsersByIdsAsync(userIds);
            var userMap = users.ToDictionary(u => u.Id, u => u);

            var response = allFolders.Select(f =>
            {
                userMap.TryGetValue(f.CreatedByUserId ?? "", out var user);

                return new FolderResponse
                {
                    Id = f.Id,
                    Name = f.Name,
                    CrateId = f.CrateId,
                    ParentFolderId = f.ParentFolderId,
                    Color = f.Color,
                    UploadedByUserId = f.CreatedByUserId ?? "",
                    UploadedByDisplayName = user?.DisplayName ?? "Unknown",
                    UploadedByEmail = user?.Email ?? "",
                    UploadedByProfilePictureUrl = user?.ProfilePictureUrl ?? "",
                    CreatedAt = f.CreatedAt
                };
            }).ToList();

            return Result<List<FolderResponse>>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetAvailableMoveFoldersAsync for CrateId {CrateId}", crateId);
            return Result<List<FolderResponse>>.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Download Folders

    public async Task<Result<FolderDownloadResult>> DownloadFolderAsync(Guid folderId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
            if (folderEntity == null)
                return Result<FolderDownloadResult>.Failure(new NotFoundError("Folder not found"));

            var permission = await _crateRoleService.CanDownload(folderEntity.CrateId, userId);
            if (!permission.IsSuccess) return Result<FolderDownloadResult>.Failure(permission.Error!);

            var allFolders = await _context.CrateFolders
                .Where(f => f.CrateId == folderEntity.CrateId && !f.IsDeleted)
                .ToListAsync();

            var allFiles = await _fileService.FetchFilesInFolderRecursivelyAsync(folderId);

            using var memoryStream = new MemoryStream();
            using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                await AddFolderToZipOptimizedAsync(folderEntity, allFolders, allFiles, zip, userId, string.Empty);
            }

            memoryStream.Position = 0;

            return Result<FolderDownloadResult>.Success(new FolderDownloadResult
            {
                FileBytes = memoryStream.ToArray(),
                FileName = folderEntity.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DownloadFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            return Result<FolderDownloadResult>.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Restore Folders

    public async Task<Result> RestoreFolderAsync(Guid folderId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders
                .Include(f => f.Subfolders)
                .FirstOrDefaultAsync(f => f.Id == folderId && f.IsDeleted);

            if (folderEntity == null) return Result.Failure(new NotFoundError("Folder not found"));

            var permission = await _crateRoleService.CanManageCrate(folderEntity.CrateId, userId);
            if (!permission.IsSuccess) return Result.Failure(permission.Error!);

            if (folderEntity.ParentFolderId.HasValue)
            {
                var current = folderEntity.ParentFolderId;
                while (current != null)
                {
                    var parent = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == current);
                    if (parent == null) return Result.Failure(new NotFoundError("Parent folder not found"));
                    if (parent.IsDeleted)
                        return Result.Failure(new AlreadyExistsError(
                            "A parent folder is deleted. Restore parent(s) first or move to root."));
                    current = parent.ParentFolderId;
                }
            }

            folderEntity.IsDeleted = false;
            folderEntity.RestoredAt = DateTime.UtcNow;
            folderEntity.RestoredByUserId = userId;
            folderEntity.UpdatedAt = DateTime.UtcNow;

            var files = await _fileService.FetchFilesInFolderRecursivelyAsync(folderEntity.Id);
            if (files.Any())
                await _fileService.RestoreFilesAsync(files.Select(f => f.Id).ToList(), userId);

            foreach (var sub in folderEntity.Subfolders)
                await RestoreFolderAsync(sub.Id, userId);

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in RestoreFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Private Helpers

    private List<Guid> GetDescendantFolderIds(List<CrateFolderEntity> allFolders, Guid parentId)
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
        CrateFolderEntity folderEntity,
        List<CrateFolderEntity> allFolders,
        List<CrateFile> allFiles,
        ZipArchive zip,
        string userId,
        string currentPath)
    {
        string folderPath = string.IsNullOrEmpty(currentPath)
            ? folderEntity.Name
            : Path.Combine(currentPath, folderEntity.Name);

        var filesInThisFolder = allFiles.Where(f => f.CrateFolderId == folderEntity.Id && !f.IsDeleted);

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

        var subfolders = allFolders.Where(f => f.ParentFolderId == folderEntity.Id);
        foreach (var sub in subfolders)
            await AddFolderToZipOptimizedAsync(sub, allFolders, allFiles, zip, userId, folderPath);
    }

    private async Task<List<CrateFolderResponse>> GetFolderItemsAsync(
        Guid crateId,
        Guid? parentFolderId,
        string? searchTerm = null)
    {
        var query = _context.CrateFolders
            .Where(f => f.CrateId == crateId && f.ParentFolderId == parentFolderId && !f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{searchTerm}%"));

        var folders = await query.OrderBy(f => f.Name).ToListAsync();

        var uploaderIds = folders
            .Select(f => f.CreatedByUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var uploaders = await _userService.GetUsersByIdsAsync(uploaderIds);

        var result = new List<CrateFolderResponse>();

        foreach (var folder in folders)
        {
            var uploader = uploaders.FirstOrDefault(u => u.Id == folder.CreatedByUserId);

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
        var folder = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId.Value && !f.IsDeleted);
        return folder?.Name ?? "Unknown";
    }

    private async Task<List<FolderBreadcrumb>> GetFolderBreadcrumbs(Guid? folderId)
    {
        var breadcrumbs = new List<FolderBreadcrumb>();

        if (!folderId.HasValue) return breadcrumbs;

        var allFolders = await _context.CrateFolders
            .Where(f => !f.IsDeleted)
            .ToListAsync();

        var currentId = folderId;

        while (currentId.HasValue)
        {
            var folder = allFolders.FirstOrDefault(f => f.Id == currentId.Value);
            if (folder == null) break;

            breadcrumbs.Insert(0, new FolderBreadcrumb
            {
                Id = folder.Id.ToString(),
                Name = folder.Name,
                Color = folder.Color ?? "#EAAC00",
                IsRoot = folder.IsRoot
            });

            currentId = folder.ParentFolderId;
        }

        return breadcrumbs;
    }

    #endregion
}