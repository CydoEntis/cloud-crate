using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Permissions;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Services.RolesAndPermissions;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Folder;

public class FolderService : IFolderService
{
    private readonly AppDbContext _context;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IFileService _fileService;
    private readonly IStorageService _storageService;
    private readonly ILogger<FolderService> _logger;

    public FolderService(
        AppDbContext context,
        ICrateRoleService crateRoleService,
        IFileService fileService,
        IStorageService storageService,
        ILogger<FolderService> logger)
    {
        _context = context;
        _crateRoleService = crateRoleService;
        _fileService = fileService;
        _storageService = storageService;
        _logger = logger;
    }


    public async Task<Result<FolderContentsResponse>> GetFolderContentsAsync(FolderContentsParameters parameters)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(parameters.UserId))
                return Result<FolderContentsResponse>.Failure(new UnauthorizedError("UserId is required"));

            var role = await _crateRoleService.GetUserRole(parameters.CrateId, parameters.UserId);
            if (role == null)
                return Result<FolderContentsResponse>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            bool searchMode = !string.IsNullOrWhiteSpace(parameters.SearchTerm);
            var searchTerm = parameters.SearchTerm?.Trim().ToLower() ?? "";

            var folders = searchMode
                ? await GetFolderItemsAsync(parameters.CrateId, parameters.FolderId, searchTerm)
                : await GetFolderItemsAsync(parameters.CrateId, parameters.FolderId);

            await AddParentFolderIfNeeded(parameters.FolderId, folders);

            var filesResult = await _fileService.GetFilesAsync(parameters);
            if (filesResult.IsFailure)
                return Result<FolderContentsResponse>.Failure(filesResult.GetError());

            var files = filesResult.GetValue();
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

    private async Task AddParentFolderIfNeeded(Guid? folderId, List<CrateFolderResponse> folders)
    {
        if (!folderId.HasValue) return;

        var currentFolder = await _context.CrateFolders
            .Include(f => f.ParentFolder)
            .FirstOrDefaultAsync(f => f.Id == folderId.Value && !f.IsDeleted);

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

    public async Task<Result<Guid>> CreateFolderAsync(CreateFolderRequest request, string userId)
    {
        try
        {
            var role = await _crateRoleService.GetUserRole(request.CrateId, userId);
            if (role == null)
                return Result<Guid>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            if (request.ParentFolderId.HasValue)
            {
                bool parentExists = await _context.CrateFolders
                    .AnyAsync(f => f.Id == request.ParentFolderId && f.CrateId == request.CrateId && !f.IsDeleted);
                if (!parentExists)
                    return Result<Guid>.Failure(new NotFoundError("Parent folder not found"));
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

    public async Task<Result> UpdateFolderAsync(Guid folderId, UpdateFolderRequest request, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
            if (folderEntity == null)
                return Result.Failure(new NotFoundError("Folder not found"));

            var role = await _crateRoleService.GetUserRole(folderEntity.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canUpdate = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => folderEntity.CreatedByUserId == userId,
                _ => false
            };

            if (!canUpdate)
                return Result.Failure(new CrateUnauthorizedError("Cannot update this folder"));

            if (request.NewName != null || request.NewColor != null)
            {
                var domainFolder = folderEntity.ToDomain();
                domainFolder.Update(request.NewName, request.NewColor);
                var updatedEntity = domainFolder.ToEntity(folderEntity.CrateId);
                _context.Entry(folderEntity).CurrentValues.SetValues(updatedEntity);
            }

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(Error.Validation(ex.Message, "Name"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateFolderAsync for FolderId {FolderId}, UserId {UserId}",
                folderId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders
                .Include(f => f.Subfolders)
                .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);

            if (folderEntity == null)
                return Result.Failure(new NotFoundError("Folder not found"));

            var role = await _crateRoleService.GetUserRole(folderEntity.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canDelete = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => folderEntity.CreatedByUserId == userId,
                _ => false
            };

            if (!canDelete)
                return Result.Failure(new CrateUnauthorizedError("Cannot delete this folder"));

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

            var role = await _crateRoleService.GetUserRole(folderEntity.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canDelete = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => folderEntity.CreatedByUserId == userId,
                _ => false
            };

            if (!canDelete)
                return Result.Failure(new CrateUnauthorizedError("Cannot delete this folder"));

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

            var role = await _crateRoleService.GetUserRole(rootFolder.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canDelete = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => rootFolder.CreatedByUserId == userId,
                _ => false
            };

            if (!canDelete)
                return Result.Failure(new CrateUnauthorizedError("Cannot delete this folder"));

            var allFolders = await _context.CrateFolders
                .Where(f => f.CrateId == rootFolder.CrateId)
                .ToListAsync();

            var folderIdsToDelete = GetDescendantFolderIds(allFolders, folderId);
            folderIdsToDelete.Add(folderId);

            var fileDeleteResult = await DeleteFilesInFoldersAsync(folderIdsToDelete, userId);
            if (fileDeleteResult.IsFailure)
            {
                await transaction.RollbackAsync();
                return fileDeleteResult;
            }

            var foldersToDelete = allFolders
                .Where(f => folderIdsToDelete.Contains(f.Id))
                .OrderByDescending(f => GetFolderDepth(f, allFolders))
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

    private async Task<Result> DeleteFilesInFoldersAsync(List<Guid> folderIds, string userId)
    {
        var fileIds = await _context.CrateFiles
            .Where(f => f.CrateFolderId != null && folderIds.Contains(f.CrateFolderId.Value))
            .Select(f => f.Id)
            .ToListAsync();

        if (!fileIds.Any())
            return Result.Success();

        return await _fileService.PermanentlyDeleteFilesAsync(fileIds, userId);
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

                if (fileResult.IsFailure)
                    return Result.Failure(fileResult.GetError());
            }

            foreach (var folderId in request.FolderIds)
            {
                var folderResult = request.Permanent
                    ? await PermanentlyDeleteFolderAsync(folderId, userId)
                    : await SoftDeleteFolderAsync(folderId, userId);

                if (folderResult.IsFailure)
                    return Result.Failure(folderResult.GetError());
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

            var filesResult = await _fileService.GetFilesInFolderRecursivelyAsync(folderEntity.Id);
            if (filesResult.IsFailure)
                return Result.Failure(filesResult.GetError());

            var files = filesResult.GetValue();
            if (files.Any())
            {
                var fileResult = await _fileService.SoftDeleteFilesAsync(files.Select(f => f.Id).ToList(), userId);
                if (fileResult.IsFailure)
                    return Result.Failure(fileResult.GetError());
            }

            foreach (var subfolder in folderEntity.Subfolders)
            {
                var subResult = await SoftDeleteFolderRecursiveAsync(subfolder, userId);
                if (subResult.IsFailure)
                    return Result.Failure(subResult.GetError());
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


    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
            if (folderEntity == null)
                return Result.Failure(new NotFoundError("Folder not found"));

            var role = await _crateRoleService.GetUserRole(folderEntity.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canMove = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => folderEntity.CreatedByUserId == userId,
                _ => false
            };

            if (!canMove)
                return Result.Failure(new CrateUnauthorizedError("Cannot move this folder"));

            var validationResult = await ValidateMoveDestinationAsync(folderEntity, newParentId);
            if (validationResult.IsFailure)
                return validationResult;

            var storageResult =
                await _storageService.MoveFolderAsync(folderEntity.CrateId, folderEntity.Id, newParentId);
            if (storageResult.IsFailure)
                return storageResult;

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

    private async Task<Result> ValidateMoveDestinationAsync(CrateFolderEntity folderEntity, Guid? newParentId)
    {
        if (!newParentId.HasValue)
            return Result.Success();

        var newParent = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == newParentId.Value && !f.IsDeleted);
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

        return Result.Success();
    }

    public async Task<Result<List<FolderResponse>>> GetAvailableMoveFoldersAsync(
        Guid crateId,
        Guid? excludeFolderId)
    {
        try
        {
            var allFolders = await _context.CrateFolders
                .Include(f => f.CreatedByUser)
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

            var domainFolders = allFolders.Select(f => f.ToDomain()).ToList();

            var response = domainFolders.Select(CrateFolderMapper.ToFolderResponse).ToList();

            return Result<List<FolderResponse>>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetAvailableMoveFoldersAsync for CrateId {CrateId}", crateId);
            return Result<List<FolderResponse>>.Failure(new InternalError(ex.Message));
        }
    }


    public async Task<Result<FolderDownloadResult>> DownloadFolderAsync(Guid folderId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
            if (folderEntity == null)
                return Result<FolderDownloadResult>.Failure(new NotFoundError("Folder not found"));

            var role = await _crateRoleService.GetUserRole(folderEntity.CrateId, userId);
            if (role == null)
                return Result<FolderDownloadResult>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var allFolders = await _context.CrateFolders
                .Where(f => f.CrateId == folderEntity.CrateId && !f.IsDeleted)
                .ToListAsync();

            var filesResult = await _fileService.GetFilesInFolderRecursivelyAsync(folderId);
            if (filesResult.IsFailure)
                return Result<FolderDownloadResult>.Failure(filesResult.GetError());

            var allFiles = filesResult.GetValue();

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


    public async Task<Result> RestoreFolderAsync(Guid folderId, string userId)
    {
        try
        {
            var folderEntity = await _context.CrateFolders
                .Include(f => f.Subfolders)
                .FirstOrDefaultAsync(f => f.Id == folderId && f.IsDeleted);

            if (folderEntity == null)
                return Result.Failure(new NotFoundError("Folder not found"));

            var role = await _crateRoleService.GetUserRole(folderEntity.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canRestore = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => folderEntity.CreatedByUserId == userId,
                _ => false
            };

            if (!canRestore)
                return Result.Failure(new CrateUnauthorizedError("Cannot restore this folder"));

            var parentValidationResult = await ValidateParentChainNotDeletedAsync(folderEntity.ParentFolderId);
            if (parentValidationResult.IsFailure)
                return parentValidationResult;

            folderEntity.IsDeleted = false;
            folderEntity.RestoredAt = DateTime.UtcNow;
            folderEntity.RestoredByUserId = userId;
            folderEntity.UpdatedAt = DateTime.UtcNow;

            var filesResult = await _fileService.GetFilesInFolderRecursivelyAsync(folderEntity.Id);
            if (filesResult.IsFailure)
                return Result.Failure(filesResult.GetError());

            var files = filesResult.GetValue();
            if (files.Any())
            {
                var fileRestoreResult = await _fileService.RestoreFilesAsync(files.Select(f => f.Id).ToList(), userId);
                if (fileRestoreResult.IsFailure)
                    return fileRestoreResult;
            }

            foreach (var subfolder in folderEntity.Subfolders)
            {
                var subfolderResult = await RestoreFolderAsync(subfolder.Id, userId);
                if (subfolderResult.IsFailure)
                    return subfolderResult;
            }

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

    public async Task<Result> EmptyTrashAsync(Guid crateId, string userId)
    {
        var role = await _crateRoleService.GetUserRole(crateId, userId);
        if (role == null)
            return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        var canEmpty = role switch
        {
            CrateRole.Owner => true,
            CrateRole.Manager => true,
            CrateRole.Member => false,
            _ => false
        };

        if (!canEmpty)
            return Result.Failure(new CrateUnauthorizedError("Cannot empty trash for this crate"));

        try
        {
            var trashedFileIds = await _context.CrateFiles
                .Where(f => f.CrateId == crateId && f.IsDeleted)
                .Select(f => f.Id)
                .ToListAsync();

            var trashedFolderIds = await _context.CrateFolders
                .Where(f => f.CrateId == crateId && f.IsDeleted)
                .Select(f => f.Id)
                .ToListAsync();

            if (trashedFileIds.Any())
            {
                var fileResult = await _fileService.PermanentlyDeleteFilesAsync(trashedFileIds, userId);
                if (fileResult.IsFailure)
                    return fileResult;
            }

            foreach (var folderId in trashedFolderIds)
            {
                var folderResult = await PermanentlyDeleteFolderAsync(folderId, userId);
                if (folderResult.IsFailure)
                    return folderResult;
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception emptying trash for CrateId {CrateId}, UserId {UserId}", crateId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    private async Task<Result> ValidateParentChainNotDeletedAsync(Guid? parentFolderId)
    {
        var current = parentFolderId;
        while (current != null)
        {
            var parent = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == current);
            if (parent == null)
                return Result.Failure(new NotFoundError("Parent folder not found"));
            if (parent.IsDeleted)
                return Result.Failure(new AlreadyExistsError(
                    "A parent folder is deleted. Restore parent(s) first or move to root."));
            current = parent.ParentFolderId;
        }

        return Result.Success();
    }


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
            var fileBytesResult = await _fileService.DownloadFileAsync(file.Id, userId);
            if (fileBytesResult.IsFailure) continue;

            string filePathInZip = Path.Combine(folderPath, file.Name).Replace("\\", "/");
            var zipEntry = zip.CreateEntry(filePathInZip, CompressionLevel.Fastest);

            using var entryStream = zipEntry.Open();
            using var fileStream = new MemoryStream(fileBytesResult.GetValue());
            await fileStream.CopyToAsync(entryStream);
        }

        var subfolders = allFolders.Where(f => f.ParentFolderId == folderEntity.Id);
        foreach (var subfolder in subfolders)
            await AddFolderToZipOptimizedAsync(subfolder, allFolders, allFiles, zip, userId, folderPath);
    }

    private async Task<List<CrateFolderResponse>> GetFolderItemsAsync(
        Guid crateId,
        Guid? parentFolderId,
        string? searchTerm = null)
    {
        var query = _context.CrateFolders
            .Include(f => f.CreatedByUser)
            .Include(f => f.ParentFolder)
            .Where(f => f.CrateId == crateId && f.ParentFolderId == parentFolderId && !f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{searchTerm}%"));

        var folderEntities = await query.OrderBy(f => f.Name).ToListAsync();

        var domainFolders = folderEntities.Select(f => f.ToDomain()).ToList();

        return domainFolders.Select(CrateFolderMapper.ToCrateFolderResponse).ToList();
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
}