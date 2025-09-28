using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Permissions;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Queries;
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

    // Constants
    private const int DefaultPageSize = 20;
    private const string DefaultFolderColor = "#EAAC00";
    private const string UnknownFolderName = "Unknown";

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

    private async Task<Result> SoftDeleteFolderRecursiveAsync(CrateFolderEntity folderEntity, string userId)
    {
        try
        {
            var filesResult = await _fileService.GetFilesInFolderRecursivelyAsync(folderEntity.Id);
            if (filesResult.IsFailure)
                return Result.Failure(filesResult.GetError());

            var files = filesResult.GetValue();

            var domainFolder = folderEntity.ToDomain();
            domainFolder.SoftDelete(userId);
            var updatedEntity = domainFolder.ToEntity(folderEntity.CrateId);
            _context.Entry(folderEntity).CurrentValues.SetValues(updatedEntity);

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

            var domainFolder = folderEntity.ToDomain();
            domainFolder.MoveTo(newParentId);

            folderEntity.ParentFolderId = domainFolder.ParentFolderId;
            folderEntity.UpdatedAt = domainFolder.UpdatedAt;

            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in MoveFolderAsync for FolderId {FolderId}, UserId {UserId}", folderId,
                userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    private async Task<Result> ValidateMoveDestinationAsync(CrateFolderEntity folderEntity, Guid? newParentId)
    {
        if (folderEntity.ParentFolderId == newParentId)
        {
            return Result.Failure(new AlreadyExistsError("Folder is already in this location"));
        }

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

    public async Task<Result<PaginatedResult<FolderResponse>>> GetAvailableMoveFoldersAsync(
        GetAvailableMoveTargetsRequest request,
        string userId)
    {
        try
        {
            var excludedIds = new HashSet<Guid>();

            // Fetch hierarchy data once for all operations
            var hierarchyData = await _context.CrateFolders
                .Where(f => f.CrateId == request.CrateId && !f.IsDeleted)
                .Select(f => new { f.Id, f.ParentFolderId })
                .ToListAsync();

            if (request.ExcludeFolderIds?.Any() == true)
            {
                foreach (var excludeFolderId in request.ExcludeFolderIds)
                {
                    var descendants = GetDescendantFolderIdsFromHierarchy(hierarchyData, excludeFolderId);
                    excludedIds.UnionWith(descendants);
                    excludedIds.Add(excludeFolderId);
                }
            }

            if (request.CurrentFolderId.HasValue)
            {
                excludedIds.Add(request.CurrentFolderId.Value);
            }

            var query = _context.CrateFolders
                .Include(f => f.CreatedByUser)
                .ApplyMoveTargetFiltering(request.CrateId, excludedIds, request.SearchTerm, request.Ascending);

            var totalCount = await query.CountAsync();

            var folders = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var domainFolders = folders.Select(f => f.ToDomain()).ToList();
            var folderResponses = domainFolders.Select(CrateFolderMapper.ToFolderResponse).ToList();

            var paginatedResult = PaginatedResult<FolderResponse>.Create(
                folderResponses,
                totalCount,
                request.Page,
                request.PageSize);

            return Result<PaginatedResult<FolderResponse>>.Success(paginatedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetAvailableMoveFoldersAsync for CrateId {CrateId}", request.CrateId);
            return Result<PaginatedResult<FolderResponse>>.Failure(new InternalError(ex.Message));
        }
    }

    private HashSet<Guid> GetDescendantFolderIdsFromHierarchy(IEnumerable<dynamic> hierarchyData, Guid parentId)
    {
        var descendants = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            var children = hierarchyData
                .Where(f => f.ParentFolderId == currentId)
                .Select(f => (Guid)f.Id);

            foreach (var childId in children)
            {
                if (descendants.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        return descendants;
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
            try
            {
                using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    await AddFolderToZipOptimizedAsync(folderEntity, allFolders, allFiles, zip, userId, string.Empty);
                }

                return Result<FolderDownloadResult>.Success(new FolderDownloadResult
                {
                    FileBytes = memoryStream.ToArray(),
                    FileName = folderEntity.Name
                });
            }
            catch
            {
                memoryStream?.Dispose();
                throw;
            }
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

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var trashedFileIds = await _context.CrateFiles
                .Where(f => f.CrateId == crateId && f.IsDeleted)
                .Select(f => f.Id)
                .ToListAsync();

            var trashedFolderIds = await _context.CrateFolders
                .Where(f => f.CrateId == crateId && f.IsDeleted)
                .OrderByDescending(f => GetFolderDepthFromId(f.Id, crateId))
                .Select(f => f.Id)
                .ToListAsync();

            if (trashedFileIds.Any())
            {
                var fileResult = await _fileService.PermanentlyDeleteFilesAsync(trashedFileIds, userId);
                if (fileResult.IsFailure)
                {
                    await transaction.RollbackAsync();
                    return fileResult;
                }
            }

            foreach (var folderId in trashedFolderIds)
            {
                var folderResult = await PermanentlyDeleteFolderAsync(folderId, userId);
                if (folderResult.IsFailure)
                {
                    await transaction.RollbackAsync();
                    return folderResult;
                }
            }

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Exception emptying trash for CrateId {CrateId}, UserId {UserId}", crateId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    private async Task<Result> ValidateParentChainNotDeletedAsync(Guid? parentFolderId)
    {
        if (!parentFolderId.HasValue)
            return Result.Success();

        var parentChain = new List<(Guid Id, string Name, bool IsDeleted, Guid? ParentId)>();
        var currentId = parentFolderId;

        while (currentId.HasValue)
        {
            var parent = await _context.CrateFolders
                .Where(f => f.Id == currentId.Value)
                .Select(f => new { f.Id, f.Name, f.IsDeleted, f.ParentFolderId })
                .FirstOrDefaultAsync();

            if (parent == null)
                return Result.Failure(new NotFoundError("Parent folder not found"));

            parentChain.Add((parent.Id, parent.Name, parent.IsDeleted, parent.ParentFolderId));

            if (parent.IsDeleted)
            {
                return Result.Failure(new AlreadyExistsError(
                    $"Parent folder '{parent.Name}' is deleted. Restore parent(s) first or move to root."));
            }

            currentId = parent.ParentFolderId;
        }

        return Result.Success();
    }

    private List<Guid> GetDescendantFolderIds(List<CrateFolderEntity> allFolders, Guid parentId)
    {
        var result = new List<Guid>();
        var toProcess = new Queue<Guid>();
        toProcess.Enqueue(parentId);

        while (toProcess.Count > 0)
        {
            var currentParentId = toProcess.Dequeue();
            var children = allFolders.Where(f => f.ParentFolderId == currentParentId);

            foreach (var child in children)
            {
                result.Add(child.Id);
                toProcess.Enqueue(child.Id);
            }
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
            try
            {
                var fileBytesResult = await _fileService.DownloadFileAsync(file.Id, userId);
                if (fileBytesResult.IsFailure)
                {
                    _logger.LogWarning("Failed to download file {FileId} for ZIP: {Error}",
                        file.Id, fileBytesResult.GetError().Message);
                    continue;
                }

                string filePathInZip = Path.Combine(folderPath, file.Name).Replace("\\", "/");
                var zipEntry = zip.CreateEntry(filePathInZip, CompressionLevel.Fastest);

                using var entryStream = zipEntry.Open();
                using var fileStream = new MemoryStream(fileBytesResult.GetValue());
                await fileStream.CopyToAsync(entryStream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add file {FileId} to ZIP", file.Id);
            }
        }

        var subfolders = allFolders.Where(f => f.ParentFolderId == folderEntity.Id && !f.IsDeleted);
        foreach (var subfolder in subfolders)
        {
            await AddFolderToZipOptimizedAsync(subfolder, allFolders, allFiles, zip, userId, folderPath);
        }
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
        {
            var normalizedSearchTerm = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{normalizedSearchTerm}%"));
        }

        var folderEntities = await query
            .OrderBy(f => f.Name)
            .ToListAsync();

        return folderEntities
            .Select(f => f.ToDomain())
            .Select(CrateFolderMapper.ToCrateFolderResponse)
            .ToList();
    }

    private async Task<string> GetFolderNameAsync(Guid? folderId)
    {
        if (!folderId.HasValue) return "Root";

        var folderName = await _context.CrateFolders
            .Where(f => f.Id == folderId.Value && !f.IsDeleted)
            .Select(f => f.Name)
            .FirstOrDefaultAsync();

        return folderName ?? UnknownFolderName;
    }

    private async Task<List<FolderBreadcrumb>> GetFolderBreadcrumbs(Guid? folderId)
    {
        if (!folderId.HasValue) return new List<FolderBreadcrumb>();

        var breadcrumbs = new List<FolderBreadcrumb>();
        var currentId = folderId;

        while (currentId.HasValue)
        {
            var folder = await _context.CrateFolders
                .Where(f => f.Id == currentId.Value && !f.IsDeleted)
                .Select(f => new { f.Id, f.Name, f.Color, f.IsRoot, f.ParentFolderId })
                .FirstOrDefaultAsync();

            if (folder == null) break;

            breadcrumbs.Insert(0, new FolderBreadcrumb
            {
                Id = folder.Id.ToString(),
                Name = folder.Name,
                Color = folder.Color ?? DefaultFolderColor,
                IsRoot = folder.IsRoot
            });

            currentId = folder.ParentFolderId;
        }

        return breadcrumbs;
    }


    public async Task<Result> BulkMoveItemsAsync(List<Guid> fileIds, List<Guid> folderIds, Guid? newParentId,
        string userId)
    {
        _logger.LogInformation(
            "BulkMoveItems starting - FileIds: {FileIds}, FolderIds: {FolderIds}, NewParentId: {NewParentId}",
            string.Join(",", fileIds ?? new List<Guid>()),
            string.Join(",", folderIds ?? new List<Guid>()),
            newParentId);

        if ((!fileIds?.Any() == true) && (!folderIds?.Any() == true))
            return Result.Success();

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var successfulMoves = new List<string>();
            var failedMoves = new List<string>();

            if (fileIds?.Any() == true)
            {
                _logger.LogInformation("Moving {Count} files to parent {ParentId}", fileIds.Count, newParentId);

                try
                {
                    var fileResult = await _fileService.MoveFilesAsync(fileIds, newParentId, userId);
                    if (fileResult.IsSuccess)
                    {
                        successfulMoves.Add($"{fileIds.Count} file(s)");
                    }
                    else
                    {
                        _logger.LogError("File move failed: {Error}", fileResult.GetError().Message);
                        failedMoves.Add($"Files: {fileResult.GetError().Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during file move operation");
                    failedMoves.Add($"Files: {ex.Message}");
                }
            }

            // FOLDERS SECTION
            if (folderIds?.Any() == true)
            {
                _logger.LogInformation("Moving {Count} folders to parent {ParentId}", folderIds.Count, newParentId);

                foreach (var folderId in folderIds)
                {
                    try
                    {
                        var folderResult = await MoveFolderAsync(folderId, newParentId, userId);
                        if (folderResult.IsSuccess)
                        {
                            successfulMoves.Add("1 folder");
                        }
                        else
                        {
                            var folderName = await GetFolderNameAsync(folderId);
                            _logger.LogError("Folder move failed for {FolderName}: {Error}", folderName,
                                folderResult.GetError().Message);
                            failedMoves.Add($"'{folderName}': {folderResult.GetError().Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var folderName = await GetFolderNameAsync(folderId);
                        _logger.LogError(ex, "Exception moving folder {FolderName}", folderName);
                        failedMoves.Add($"'{folderName}': {ex.Message}");
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (failedMoves.Any() && successfulMoves.Any())
            {
                var successMsg = $"Successfully moved {string.Join(", ", successfulMoves)}.";
                var failMsg = $"Failed to move: {string.Join("; ", failedMoves)}";
                return Result.Failure(new ValidationError($"{successMsg} {failMsg}"));
            }
            else if (failedMoves.Any())
            {
                return Result.Failure(new ValidationError($"Failed to move: {string.Join("; ", failedMoves)}"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Exception in BulkMoveItemsAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> BulkSoftDeleteItemsAsync(List<Guid> fileIds, List<Guid> folderIds, string userId)
    {
        if ((!fileIds?.Any() == true) && (!folderIds?.Any() == true))
            return Result.Success();

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (fileIds?.Any() == true)
            {
                var fileResult = await _fileService.SoftDeleteFilesAsync(fileIds, userId);
                if (fileResult.IsFailure)
                {
                    await transaction.RollbackAsync();
                    return fileResult;
                }
            }

            if (folderIds?.Any() == true)
            {
                var failedDeletes = new List<string>();

                foreach (var folderId in folderIds)
                {
                    var result = await SoftDeleteFolderAsync(folderId, userId);
                    if (result.IsFailure)
                    {
                        var folderName = await GetFolderNameAsync(folderId);
                        failedDeletes.Add($"'{folderName}': {result.GetError().Message}");
                    }
                }

                if (failedDeletes.Any())
                {
                    await transaction.RollbackAsync();
                    return Result.Failure(new ValidationError(
                        $"Failed to delete some folders: {string.Join("; ", failedDeletes)}"));
                }
            }

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Exception in BulkSoftDeleteItemsAsync for UserId {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    private async Task<int> GetFolderDepthFromId(Guid folderId, Guid crateId)
    {
        int depth = 0;
        Guid? currentId = folderId; // Make it nullable

        while (currentId.HasValue)
        {
            var parentId = await _context.CrateFolders
                .Where(f => f.Id == currentId.Value && f.CrateId == crateId)
                .Select(f => f.ParentFolderId)
                .FirstOrDefaultAsync();

            if (!parentId.HasValue) break;

            depth++;
            currentId = parentId;
        }

        return depth;
    }
}