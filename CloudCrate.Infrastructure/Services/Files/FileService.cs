using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.User.Mappers;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Files;

public sealed record FileTooLargeError(string Message = "The uploaded file exceeds the maximum allowed size (10 MB)") : Error(Message);
public sealed record VideoNotAllowedError(string Message = "Video files are not allowed") : Error(Message);
public sealed record StorageQuotaExceededError(string Message = "Uploading this file would exceed your storage quota") : Error(Message);

public class FileService : IFileService
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IUserService _userService;
    private readonly ILogger<FileService> _logger;

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public FileService(
        AppDbContext context,
        IStorageService storageService,
        ICrateRoleService crateRoleService,
        IUserService userService,
        ILogger<FileService> logger)
    {
        _context = context;
        _storageService = storageService;
        _crateRoleService = crateRoleService;
        _userService = userService;
        _logger = logger;
    }

    public async Task<List<FileTypeBreakdownDto>> GetFilesByMimeTypeAsync(Guid crateId)
    {
        try
        {
            return await _context.CrateFiles
                .Where(f => f.CrateId == crateId)
                .GroupBy(f => MimeCategoryHelper.GetMimeCategory(f.MimeType))
                .Select(g => new FileTypeBreakdownDto
                {
                    Type = g.Key,
                    SizeMb = Math.Round(g.Sum(f => (long?)f.SizeInBytes ?? 0) / 1024.0 / 1024.0, 2)
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file breakdown for CrateId: {CrateId}", crateId);
            throw;
        }
    }

    public List<FileTypeBreakdownDto> GetFilesByMimeTypeInMemory(IEnumerable<CrateFile> files)
    {
        try
        {
            return files
                .GroupBy(f => MimeCategoryHelper.GetMimeCategory(f.MimeType))
                .Select(g => new FileTypeBreakdownDto
                {
                    Type = g.Key,
                    SizeMb = Math.Round(g.Sum(f => (long?)f.Size.Bytes ?? 0) / 1024.0 / 1024.0, 2)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute in-memory file breakdown");
            throw;
        }
    }

    #region Fetch Files

    public async Task<Result<CrateFileResponse>> FetchFileResponseAsync(Guid fileId, string userId)
    {
        try
        {
            var fileResult = await FetchAuthorizedFileAsync(fileId, userId);
            if (fileResult.IsFailure)
            {
                _logger.LogWarning("FetchFileResponseAsync failed: FileId {FileId}, UserId {UserId}, Error {Error}",
                    fileId, userId, fileResult.Error?.Message);
                return Result<CrateFileResponse>.Failure(fileResult.Error!);
            }

            var fileResponse = await MapFileWithUploaderAsync(fileResult.Value!, userId);
            return Result<CrateFileResponse>.Success(fileResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FetchFileResponseAsync for FileId {FileId}, UserId {UserId}", fileId,
                userId);
            return Result<CrateFileResponse>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<byte[]>> FetchFileBytesAsync(Guid fileId, string userId)
    {
        try
        {
            var fileResult = await FetchAuthorizedFileAsync(fileId, userId);
            if (fileResult.IsFailure)
            {
                _logger.LogWarning("FetchFileBytesAsync failed: FileId {FileId}, UserId {UserId}, Error {Error}",
                    fileId, userId, fileResult.Error?.Message);
                return Result<byte[]>.Failure(fileResult.Error!);
            }

            var file = fileResult.Value!;
            var bytesResult = await _storageService.ReadFileAsync(file.CrateId, file.CrateFolderId, file.Name);

            if (bytesResult.IsFailure)
            {
                _logger.LogWarning("ReadFileAsync failed: FileId {FileId}, UserId {UserId}, Error {Error}",
                    fileId, userId, bytesResult.Error?.Message);
                return Result<byte[]>.Failure(bytesResult.Error!);
            }

            return Result<byte[]>.Success(bytesResult.Value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FetchFileBytesAsync for FileId {FileId}, UserId {UserId}", fileId,
                userId);
            return Result<byte[]>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<PaginatedResult<CrateFileResponse>> FetchFilesAsync(FolderContentsParameters parameters)
    {
        try
        {
            var query = BuildFileQuery(parameters);
            var pagedFiles = await query.PaginateAsync(parameters.Page, parameters.PageSize);

            var files = new List<CrateFileResponse>();
            foreach (var file in pagedFiles.Items)
            {
                files.Add(await MapFileWithUploaderAsync(file, parameters.UserId));
            }

            return new PaginatedResult<CrateFileResponse>
            {
                Items = files,
                TotalCount = pagedFiles.TotalCount,
                Page = pagedFiles.Page,
                PageSize = pagedFiles.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FetchFilesAsync for CrateId {CrateId}, UserId {UserId}",
                parameters.CrateId, parameters.UserId);
            throw;
        }
    }

    public async Task<List<CrateFile>> FetchFilesInFolderRecursivelyAsync(Guid folderId)
    {
        try
        {
            var allFiles = new List<CrateFileEntity>();
            var foldersToProcess = new Queue<Guid>();
            foldersToProcess.Enqueue(folderId);

            while (foldersToProcess.Count > 0)
            {
                var currentFolderId = foldersToProcess.Dequeue();

                var filesInFolder = await _context.CrateFiles
                    .Where(f => f.CrateFolderId == currentFolderId && !f.IsDeleted)
                    .ToListAsync();

                allFiles.AddRange(filesInFolder);

                var subfolders = await _context.CrateFolders
                    .Where(f => f.ParentFolderId == currentFolderId && !f.IsDeleted)
                    .Select(f => f.Id)
                    .ToListAsync();

                foreach (var subId in subfolders)
                    foldersToProcess.Enqueue(subId);
            }

            return allFiles.Select(f => f.ToDomain()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FetchFilesInFolderRecursivelyAsync for FolderId {FolderId}", folderId);
            throw;
        }
    }

    public async Task<long> FetchTotalFileSizeInFolderAsync(Guid folderId)
    {
        try
        {
            return await _context.CrateFiles
                .Where(f => f.CrateFolderId == folderId && !f.IsDeleted)
                .SumAsync(f => (long?)f.SizeInBytes) ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FetchTotalFileSizeInFolderAsync for FolderId {FolderId}", folderId);
            throw;
        }
    }

    #endregion

    #region Upload Files

    public async Task<Result<List<Guid>>> UploadFilesAsync(MultiFileUploadRequest request, string userId)
    {
        if (request.Files == null || !request.Files.Any())
            return Result<List<Guid>>.Failure(Error.Validation("No files provided.", "Files"));

        foreach (var fileReq in request.Files)
        {
            var validation = ValidateFileUpload(fileReq);
            if (validation.IsFailure)
                return Result<List<Guid>>.Failure(validation.Error!);
        }

        var crateId = request.Files.First().CrateId;
        var permissionCheck = await _crateRoleService.CanUpload(crateId, userId);
        if (permissionCheck.IsFailure)
            return Result<List<Guid>>.Failure(permissionCheck.Error!);

        var totalSize = request.Files.Sum(f => f.SizeInBytes);
        var userResult = await _userService.GetUserByIdAsync(userId);
        if (userResult.IsFailure || userResult.Value == null)
            return Result<List<Guid>>.Failure(Error.NotFound("User not found"));

        var user = userResult.Value;
        if (user.UsedAccountStorageBytes + totalSize > user.AllocatedStorageLimitBytes)
            return Result<List<Guid>>.Failure(new StorageQuotaExceededError());

        var storageResults = new List<Result<string>>();
        var fileEntities = new List<CrateFileEntity>();

        try
        {
            foreach (var req in request.Files)
            {
                if (req.FolderId == null || req.FolderId == Guid.Empty)
                    return Result<List<Guid>>.Failure(
                        Error.Validation($"FolderId is required for file {req.FileName}.", "FolderId"));

                var folderExists = await _context.CrateFolders
                    .AnyAsync(f => f.Id == req.FolderId.Value && f.CrateId == req.CrateId);
                if (!folderExists)
                    return Result<List<Guid>>.Failure(Error.NotFound($"Folder not found for file {req.FileName}"));
            }

            foreach (var fileReq in request.Files)
            {
                var result = await _storageService.SaveFileAsync(fileReq);
                if (result.IsFailure)
                {
                    await CleanupStorageFiles(storageResults, request.Files);
                    _logger.LogWarning("UploadFilesAsync storage failed: UserId {UserId}, Error {Error}", userId,
                        result.Error?.Message);
                    return Result<List<Guid>>.Failure(result.Error!);
                }

                storageResults.Add(result);
            }

            for (int i = 0; i < request.Files.Count; i++)
            {
                var req = request.Files[i];
                var domainFile = CrateFile.Create(
                    req.FileName, 
                    StorageSize.FromBytes(req.SizeInBytes), 
                    req.MimeType, 
                    req.CrateId, 
                    userId,
                    req.FolderId);
                
                domainFile.SetObjectKey(storageResults[i].Value!);
                
                var fileEntity = domainFile.ToEntity(req.CrateId);
                fileEntities.Add(fileEntity);
            }

            var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
            if (crateEntity == null)
            {
                await CleanupStorageFiles(storageResults, request.Files);
                return Result<List<Guid>>.Failure(Error.NotFound("Crate not found."));
            }

            var crate = crateEntity.ToDomain();
            if (crate.RemainingStorage.Bytes < totalSize)
            {
                await CleanupStorageFiles(storageResults, request.Files);
                return Result<List<Guid>>.Failure(Error.Validation("Crate storage limit exceeded.", "Storage"));
            }

            crate.ConsumeStorage(StorageSize.FromBytes(totalSize));

            var incrementResult = await _userService.IncrementUsedStorageAsync(userId, totalSize);
            if (incrementResult.IsFailure)
            {
                crate.ReleaseStorage(StorageSize.FromBytes(totalSize));
                await CleanupStorageFiles(storageResults, request.Files);
                return Result<List<Guid>>.Failure(incrementResult.Error!);
            }

            crateEntity.UpdateEntity(crate);

            _context.CrateFiles.AddRange(fileEntities);
            await _context.SaveChangesAsync();

            return Result<List<Guid>>.Success(fileEntities.Select(f => f.Id).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UploadFilesAsync for UserId {UserId}", userId);
            await CleanupStorageFiles(storageResults, request.Files);
            return Result<List<Guid>>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<Guid>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        try
        {
            var validation = ValidateFileUpload(request);
            if (validation.IsFailure)
            {
                _logger.LogWarning("UploadFileAsync validation failed: UserId {UserId}, Error {Error}", userId,
                    validation.Error?.Message);
                return Result<Guid>.Failure(validation.Error!);
            }

            var permissionCheck = await _crateRoleService.CanUpload(request.CrateId, userId);
            if (permissionCheck.IsFailure)
                return Result<Guid>.Failure(permissionCheck.Error!);

            if (request.FolderId.HasValue)
            {
                var folderExists = await _context.CrateFolders
                    .AnyAsync(f => f.Id == request.FolderId.Value && f.CrateId == request.CrateId);
                if (!folderExists)
                    return Result<Guid>.Failure(Error.NotFound("Folder not found"));
            }

            var userResult = await _userService.GetUserByIdAsync(userId);
            if (userResult.IsFailure || userResult.Value == null)
                return Result<Guid>.Failure(Error.NotFound("User not found"));

            var user = userResult.Value;
            if (user.UsedAccountStorageBytes + request.SizeInBytes > user.AllocatedStorageLimitBytes)
                return Result<Guid>.Failure(new StorageQuotaExceededError());

            var saveResult = await _storageService.SaveFileAsync(request);
            if (saveResult.IsFailure)
            {
                _logger.LogWarning("UploadFileAsync storage failed: UserId {UserId}, Error {Error}", userId,
                    saveResult.Error?.Message);
                return Result<Guid>.Failure(saveResult.Error!);
            }

            var domainFile = CrateFile.Create(
                request.FileName,
                StorageSize.FromBytes(request.SizeInBytes),
                request.MimeType,
                request.CrateId,
                userId,
                request.FolderId
            );
            
            domainFile.SetObjectKey(saveResult.Value!);
            var fileEntity = domainFile.ToEntity(request.CrateId);

            var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == request.CrateId);
            if (crateEntity == null)
            {
                await _storageService.DeleteFileAsync(request.CrateId, request.FolderId, request.FileName);
                _logger.LogWarning("UploadFileAsync failed: Crate not found. CrateId {CrateId}, UserId {UserId}",
                    request.CrateId, userId);
                return Result<Guid>.Failure(Error.NotFound("Crate not found"));
            }

            var crate = crateEntity.ToDomain();
            if (crate.RemainingStorage.Bytes < request.SizeInBytes)
            {
                await _storageService.DeleteFileAsync(request.CrateId, request.FolderId, request.FileName);
                _logger.LogWarning(
                    "UploadFileAsync failed: Crate storage limit exceeded. CrateId {CrateId}, UserId {UserId}",
                    crate.Id, userId);
                return Result<Guid>.Failure(Error.Validation("Crate storage limit exceeded.", "Storage"));
            }

            crate.ConsumeStorage(StorageSize.FromBytes(request.SizeInBytes));

            var storageResult = await _userService.IncrementUsedStorageAsync(userId, request.SizeInBytes);
            if (storageResult.IsFailure)
            {
                crate.ReleaseStorage(StorageSize.FromBytes(request.SizeInBytes));
                await _storageService.DeleteFileAsync(request.CrateId, request.FolderId, request.FileName);
                _logger.LogWarning(
                    "UploadFileAsync failed: User storage increment failed. UserId {UserId}, Error {Error}", userId,
                    storageResult.Error?.Message);
                return Result<Guid>.Failure(storageResult.Error!);
            }

            crateEntity.UpdateEntity(crate);

            _context.CrateFiles.Add(fileEntity);
            await _context.SaveChangesAsync();

            return Result<Guid>.Success(fileEntity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UploadFileAsync for UserId {UserId}", userId);
            return Result<Guid>.Failure(new InternalError(ex.Message));
        }
    }

    #endregion

    #region Delete / Restore / Move Files

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        try
        {
            var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null) return Result<byte[]>.Failure(new FileNotFoundError());

            var permissionCheck = await _crateRoleService.CanDownload(file.CrateId, userId);
            if (permissionCheck.IsFailure) return Result<byte[]>.Failure(permissionCheck.Error!);

            var fileResult = await _storageService.ReadFileAsync(file.CrateId, file.CrateFolderId, file.Name);
            if (fileResult.IsFailure) return Result<byte[]>.Failure(fileResult.Error!);

            return Result<byte[]>.Success(fileResult.Value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DownloadFileAsync for FileId {FileId}, UserId {UserId}", fileId, userId);
            return Result<byte[]>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, string userId)
    {
        try
        {
            var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null) return Result.Failure(new FileNotFoundError());

            var deletePermission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
            if (deletePermission.IsFailure) return Result.Failure(deletePermission.Error!);

            var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == file.CrateId);
            if (crateEntity == null) return Result.Failure(Error.NotFound("Crate not found"));

            var deleteResult =
                await _storageService.DeleteFileAsync(file.CrateId, file.CrateFolderId, file.Name);
            if (deleteResult.IsFailure) return Result.Failure(deleteResult.Error!);

            // Convert to domain to use business logic
            var crate = crateEntity.ToDomain();
            crate.ReleaseStorage(StorageSize.FromBytes(file.SizeInBytes));

            var storageResult = await _userService.DecrementUsedStorageAsync(userId, file.SizeInBytes);
            if (storageResult.IsFailure) return Result.Failure(storageResult.Error!);

            // Update EF entity with domain changes
            crateEntity.UpdateEntity(crate);

            _context.CrateFiles.Remove(file);
            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeleteFileAsync for FileId {FileId}, UserId {UserId}", fileId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> SoftDeleteFileAsync(Guid fileId, string userId)
    {
        try
        {
            var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null) return Result.Failure(new FileNotFoundError());

            var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
            if (permission.IsFailure) return Result.Failure(permission.Error!);

            var domainFile = file.ToDomain();
            domainFile.SoftDelete(userId);

            file.IsDeleted = domainFile.IsDeleted;
            file.DeletedAt = domainFile.DeletedAt;
            file.DeletedByUserId = domainFile.DeletedByUserId;
            file.UpdatedAt = domainFile.UpdatedAt;

            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in SoftDeleteFileAsync for FileId {FileId}, UserId {UserId}", fileId,
                userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> SoftDeleteFilesAsync(List<Guid> fileIds, string userId)
    {
        foreach (var fileId in fileIds)
        {
            var result = await SoftDeleteFileAsync(fileId, userId);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteFilesAsync(List<Guid> fileIds, string userId)
    {
        foreach (var fileId in fileIds)
        {
            try
            {
                var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
                if (file == null) continue;

                var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
                if (permission.IsFailure) return Result.Failure(permission.Error!);

                var storageResult = await _storageService.DeleteFileAsync(file.CrateId, file.CrateFolderId, file.Name);
                if (storageResult.IsFailure) return Result.Failure(storageResult.Error!);

                var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == file.CrateId);
                if (crateEntity != null)
                {
                    var crate = crateEntity.ToDomain();
                    crate.ReleaseStorage(StorageSize.FromBytes(file.SizeInBytes));
                    crateEntity.UpdateEntity(crate);
                }

                var userStorageResult = await _userService.DecrementUsedStorageAsync(userId, file.SizeInBytes);
                if (userStorageResult.IsFailure) return Result.Failure(userStorageResult.Error!);

                _context.CrateFiles.Remove(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in PermanentlyDeleteFilesAsync for FileId {FileId}, UserId {UserId}",
                    fileId, userId);
                return Result.Failure(new InternalError(ex.Message));
            }
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteFilesInFolderRecursivelyAsync(Guid folderId, string userId)
    {
        try
        {
            var files = await _context.CrateFiles.Where(f => f.CrateFolderId == folderId).ToListAsync();
            foreach (var file in files)
            {
                var result = await DeleteFileAsync(file.Id, userId);
                if (result.IsFailure) return result;
            }

            var subfolders = await _context.CrateFolders.Where(f => f.ParentFolderId == folderId).ToListAsync();
            foreach (var sub in subfolders)
            {
                var result = await DeleteFilesInFolderRecursivelyAsync(sub.Id, userId);
                if (result.IsFailure) return result;
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception in DeleteFilesInFolderRecursivelyAsync for FolderId {FolderId}, UserId {UserId}", folderId,
                userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId)
    {
        try
        {
            var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null) return Result.Failure(new FileNotFoundError());

            var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
            if (permission.IsFailure) return Result.Failure(permission.Error!);

            if (newParentId.HasValue && newParentId.Value == Guid.Empty) newParentId = null;

            if (newParentId.HasValue)
            {
                var folderExists = await _context.CrateFolders
                    .AnyAsync(f => f.Id == newParentId.Value && f.CrateId == file.CrateId);
                if (!folderExists) return Result.Failure(new NotFoundError("Destination folder not found"));
            }

            var storageResult = await _storageService.MoveFileAsync(
                file.CrateId,
                file.CrateFolderId,
                newParentId,
                file.Name
            );
            if (!storageResult.IsSuccess) return Result.Failure(storageResult.Error!);

            // Convert to domain, apply business logic, then update entity
            var domainFile = file.ToDomain();
            domainFile.MoveTo(newParentId);

            // Update EF entity properties
            file.CrateFolderId = domainFile.CrateFolderId;
            file.UpdatedAt = domainFile.UpdatedAt;

            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in MoveFileAsync for FileId {FileId}, UserId {UserId}", fileId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> MoveFilesAsync(IEnumerable<Guid> fileIds, Guid? newParentId, string userId)
    {
        foreach (var fileId in fileIds)
        {
            var result = await MoveFileAsync(fileId, newParentId, userId);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    public async Task<Result> RestoreFileAsync(Guid fileId, string userId)
    {
        try
        {
            var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null) return Result.Failure(new FileNotFoundError());

            var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
            if (permission.IsFailure) return Result.Failure(permission.Error!);

            if (file.CrateFolderId.HasValue)
            {
                var parent = await _context.CrateFolders.FirstOrDefaultAsync(f => f.Id == file.CrateFolderId.Value);
                if (parent == null) return Result.Failure(new NotFoundError("Parent folder not found"));
                if (parent.IsDeleted)
                    return Result.Failure(new FileError("Parent folder is deleted. Restore parent or move to root."));
            }

            var domainFile = file.ToDomain();
            domainFile.Restore(userId);

            file.IsDeleted = domainFile.IsDeleted;
            file.RestoredAt = domainFile.RestoredAt;
            file.RestoredByUserId = domainFile.RestoredByUserId;
            file.UpdatedAt = domainFile.UpdatedAt;

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in RestoreFileAsync for FileId {FileId}, UserId {UserId}", fileId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> RestoreFilesAsync(List<Guid> fileIds, string userId)
    {
        foreach (var fileId in fileIds)
        {
            var result = await RestoreFileAsync(fileId, userId);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    #endregion

    #region Helpers

    private Result ValidateFileUpload(FileUploadRequest request)
    {
        if (request.SizeInBytes > MaxFileSize)
            return Result.Failure(new FileTooLargeError());

        if (request.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new VideoNotAllowedError());

        return Result.Success();
    }

    private async Task CleanupStorageFiles(List<Result<string>> storageResults, List<FileUploadRequest> files)
    {
        for (int i = 0; i < storageResults.Count; i++)
        {
            if (storageResults[i].IsSuccess)
            {
                var uploadedFile = files[i];
                await _storageService.DeleteFileAsync(uploadedFile.CrateId, uploadedFile.FolderId,
                    uploadedFile.FileName);
            }
        }
    }

    private async Task<Result<CrateFileEntity>> FetchAuthorizedFileAsync(Guid fileId, string userId)
    {
        try
        {
            var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
            if (file == null)
            {
                _logger.LogWarning("FetchAuthorizedFileAsync: File not found. FileId {FileId}, UserId {UserId}", fileId,
                    userId);
                return Result<CrateFileEntity>.Failure(new FileNotFoundError());
            }

            var permission = await _crateRoleService.CanView(file.CrateId, userId);
            if (permission.IsFailure)
            {
                _logger.LogWarning(
                    "FetchAuthorizedFileAsync: Permission denied. FileId {FileId}, UserId {UserId}, Error {Error}",
                    fileId, userId, permission.Error?.Message);
                return Result<CrateFileEntity>.Failure(permission.Error!);
            }

            return Result<CrateFileEntity>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FetchAuthorizedFileAsync for FileId {FileId}, UserId {UserId}", fileId,
                userId);
            return Result<CrateFileEntity>.Failure(new InternalError(ex.Message));
        }
    }

    private async Task<CrateFileResponse> MapFileWithUploaderAsync(CrateFileEntity file, string currentUserId)
    {
        try
        {
            var userResult = await _userService.GetUserByIdAsync(file.UploadedByUserId);
            UserResponse? user = null;
            if (userResult.IsSuccess && userResult.Value != null)
            {
                user = userResult.Value;
            }

            var urlResult =
                await _storageService.GetFileUrlAsync(file.CrateId, file.CrateFolderId, file.Name);

            // Convert to domain entity first, then to response
            var domainFile = file.ToDomain();
            return CrateFileDomainMapper.ToCrateFileResponse(
                domainFile,
                user != null ? UserMapper.ToUploader(user) : null,
                urlResult.IsSuccess ? urlResult.Value : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in MapFileWithUploaderAsync for FileId {FileId}, UserId {UserId}", file.Id,
                currentUserId);
            throw;
        }
    }

    private IQueryable<CrateFileEntity> BuildFileQuery(FolderContentsParameters parameters)
    {
        var query = _context.CrateFiles
            .Include(f => f.CrateFolder)
            .Where(f => f.CrateId == parameters.CrateId && !f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{parameters.SearchTerm}%"));

        if (parameters.FolderId.HasValue)
            query = query.Where(f => f.CrateFolderId == parameters.FolderId);
        else if (string.IsNullOrWhiteSpace(parameters.SearchTerm))
            query = query.Where(f => f.CrateFolderId == null);

        query = ApplyFilters(query, parameters.MinSize, parameters.MaxSize, parameters.CreatedAfter,
            parameters.CreatedBefore);
        return ApplyOrdering(query, parameters.OrderBy, parameters.Ascending);
    }

    private IQueryable<CrateFileEntity> ApplyFilters(IQueryable<CrateFileEntity> query, long? minSize, long? maxSize,
        DateTime? createdAfter, DateTime? createdBefore)
    {
        if (minSize.HasValue) query = query.Where(f => f.SizeInBytes >= minSize.Value);
        if (maxSize.HasValue) query = query.Where(f => f.SizeInBytes <= maxSize.Value);
        if (createdAfter.HasValue) query = query.Where(f => f.CreatedAt >= createdAfter.Value);
        if (createdBefore.HasValue) query = query.Where(f => f.CreatedAt <= createdBefore.Value);
        return query;
    }

    private IQueryable<CrateFileEntity> ApplyOrdering(IQueryable<CrateFileEntity> query, OrderBy orderBy, bool ascending)
    {
        return orderBy switch
        {
            OrderBy.Name => ascending ? query.OrderBy(f => f.Name) : query.OrderByDescending(f => f.Name),
            OrderBy.Size => ascending
                ? query.OrderBy(f => f.SizeInBytes)
                : query.OrderByDescending(f => f.SizeInBytes),
            OrderBy.CreatedAt => ascending
                ? query.OrderBy(f => f.CreatedAt)
                : query.OrderByDescending(f => f.CreatedAt),
            _ => query.OrderBy(f => f.Name)
        };
    }

    #endregion
}