using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.User.Mappers;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Queries;
using CloudCrate.Infrastructure.Services.RolesAndPermissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Files;

public sealed record FileTooLargeError(string Message = "The uploaded file exceeds the maximum allowed size (10 MB)")
    : Error(Message);

public sealed record VideoNotAllowedError(string Message = "Video files are not allowed") : Error(Message);

public class FileService : IFileService
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IUserService _userService;
    private readonly ILogger<FileService> _logger;

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private const string VIDEO_MIME_PREFIX = "video/";
    private const string FOLDER_NOT_FOUND_MESSAGE = "Folder not found";
    private const string CRATE_NOT_FOUND_MESSAGE = "Crate not found";
    private const string DESTINATION_FOLDER_NOT_FOUND_MESSAGE = "Destination folder not found";

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


    public async Task<Result<Guid>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        if (request.SizeInBytes > MaxFileSize)
            return Result<Guid>.Failure(new FileTooLargeError());

        if (request.MimeType.StartsWith(VIDEO_MIME_PREFIX, StringComparison.OrdinalIgnoreCase))
            return Result<Guid>.Failure(new VideoNotAllowedError());

        var role = await _crateRoleService.GetUserRole(request.CrateId, userId);
        if (role == null)
            return Result<Guid>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        if (request.FolderId.HasValue)
        {
            var folderExists = await _context.CrateFolders
                .AnyAsync(f => f.Id == request.FolderId.Value && f.CrateId == request.CrateId);
            if (!folderExists)
                return Result<Guid>.Failure(Error.NotFound(FOLDER_NOT_FOUND_MESSAGE));
        }

        var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == request.CrateId);
        if (crateEntity == null)
            return Result<Guid>.Failure(Error.NotFound(CRATE_NOT_FOUND_MESSAGE));

        var crate = crateEntity.ToDomain();
        if (crate.RemainingStorage.Bytes < request.SizeInBytes)
            return Result<Guid>.Failure(Error.Validation("Crate storage limit exceeded.", "Storage"));

        var crateOwner = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == request.CrateId && m.Role == CrateRole.Owner);

        if (crateOwner == null)
            return Result<Guid>.Failure(Error.NotFound("Crate owner not found"));

        var crateOwnerId = crateOwner.UserId;

        var userCanConsume = await _userService.CanConsumeStorageAsync(crateOwnerId, request.SizeInBytes);
        if (userCanConsume.IsFailure)
            return Result<Guid>.Failure(userCanConsume.GetError());

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var storageResult = await _storageService.SaveFileAsync(request);
            if (storageResult.IsFailure)
            {
                await transaction.RollbackAsync();
                return Result<Guid>.Failure(storageResult.GetError());
            }

            var domainFile = CrateFile.Create(
                request.FileName,
                StorageSize.FromBytes(request.SizeInBytes),
                request.MimeType,
                request.CrateId,
                userId,
                request.FolderId);

            domainFile.SetObjectKey(storageResult.GetValue());

            crate.ConsumeStorage(StorageSize.FromBytes(request.SizeInBytes));
            crateEntity.UpdateEntity(crate);

            var userStorageResult = await _userService.IncrementUsedStorageAsync(crateOwnerId, request.SizeInBytes);
            if (userStorageResult.IsFailure)
            {
                await transaction.RollbackAsync();
                await _storageService.DeleteFileAsync(request.CrateId, request.FolderId, request.FileName);
                return Result<Guid>.Failure(userStorageResult.GetError());
            }

            var fileEntity = domainFile.ToEntity(request.CrateId);
            _context.CrateFiles.Add(fileEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Successfully uploaded file {FileId} for user {UserId}", fileEntity.Id, userId);
            return Result<Guid>.Success(fileEntity.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Exception uploading file for UserId {UserId}", userId);
            return Result<Guid>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<List<Guid>>> UploadFilesAsync(MultiFileUploadRequest request, string userId)
    {
        if (request.Files == null || !request.Files.Any())
            return Result<List<Guid>>.Failure(Error.Validation("No files provided.", "Files"));

        foreach (var fileReq in request.Files)
        {
            if (fileReq.SizeInBytes > MaxFileSize)
                return Result<List<Guid>>.Failure(new FileTooLargeError());

            if (fileReq.MimeType.StartsWith(VIDEO_MIME_PREFIX, StringComparison.OrdinalIgnoreCase))
                return Result<List<Guid>>.Failure(new VideoNotAllowedError());
        }

        var crateId = request.Files.First().CrateId;
        var role = await _crateRoleService.GetUserRole(crateId, userId);
        if (role == null)
            return Result<List<Guid>>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        var totalSize = request.Files.Sum(f => f.SizeInBytes);

        foreach (var req in request.Files)
        {
            if (req.FolderId == null || req.FolderId == Guid.Empty)
                return Result<List<Guid>>.Failure(Error.Validation($"FolderId is required for file {req.FileName}.",
                    "FolderId"));

            var folderExists = await _context.CrateFolders
                .AnyAsync(f => f.Id == req.FolderId.Value && f.CrateId == req.CrateId);
            if (!folderExists)
                return Result<List<Guid>>.Failure(Error.NotFound($"Folder not found for file {req.FileName}"));
        }

        var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
        if (crateEntity == null)
            return Result<List<Guid>>.Failure(Error.NotFound(CRATE_NOT_FOUND_MESSAGE));

        var crate = crateEntity.ToDomain();
        if (crate.RemainingStorage.Bytes < totalSize)
            return Result<List<Guid>>.Failure(Error.Validation("Crate storage limit exceeded.", "Storage"));

        var crateOwner = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.Role == CrateRole.Owner);

        if (crateOwner == null)
            return Result<List<Guid>>.Failure(Error.NotFound("Crate owner not found"));

        var crateOwnerId = crateOwner.UserId;

        var userCanConsume = await _userService.CanConsumeStorageAsync(crateOwnerId, totalSize);
        if (userCanConsume.IsFailure)
            return Result<List<Guid>>.Failure(userCanConsume.GetError());

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var storageResult = await _storageService.SaveFilesAsync(request.Files);
            if (storageResult.IsFailure)
            {
                await transaction.RollbackAsync();
                return Result<List<Guid>>.Failure(storageResult.GetError());
            }

            var uploadedKeys = storageResult.GetValue();

            var fileEntities = new List<CrateFileEntity>();
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

                domainFile.SetObjectKey(uploadedKeys[i]);
                fileEntities.Add(domainFile.ToEntity(req.CrateId));
            }

            crate.ConsumeStorage(StorageSize.FromBytes(totalSize));
            crateEntity.UpdateEntity(crate);

            var userStorageResult = await _userService.IncrementUsedStorageAsync(crateOwnerId, totalSize);
            if (userStorageResult.IsFailure)
            {
                await transaction.RollbackAsync();
                return Result<List<Guid>>.Failure(userStorageResult.GetError());
            }

            _context.CrateFiles.AddRange(fileEntities);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var fileIds = fileEntities.Select(f => f.Id).ToList();
            _logger.LogInformation("Successfully uploaded {Count} files for user {UserId}", fileIds.Count, userId);
            return Result<List<Guid>>.Success(fileIds);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Exception uploading multiple files for UserId {UserId}", userId);
            return Result<List<Guid>>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<CrateFileResponse>> GetFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles
            .Include(f => f.UploadedByUser)
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null)
            return Result<CrateFileResponse>.Failure(new FileNotFoundError());

        var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
        if (role == null)
            return Result<CrateFileResponse>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        try
        {
            var urlResult = await _storageService.GetFileUrlAsync(file.CrateId, file.CrateFolderId, file.Name);
            var domainFile = file.ToDomain();

            var fileResponse = CrateFileDomainMapper.ToCrateFileResponse(
                domainFile,
                domainFile.UploadedByUser != null ? UserMapper.ToUploader(domainFile.UploadedByUser) : null,
                urlResult.IsSuccess ? urlResult.GetValue() : null
            );

            return Result<CrateFileResponse>.Success(fileResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception mapping file {FileId} for user {UserId}", fileId, userId);
            return Result<CrateFileResponse>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null)
            return Result<byte[]>.Failure(new FileNotFoundError());

        var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
        if (role == null)
            return Result<byte[]>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        _logger.LogInformation("Attempting to download file - ObjectKey: {ObjectKey}, Expected size: {Size}",
            file.ObjectKey, file.SizeInBytes);

        var result = await _storageService.ReadFileByKeyAsync(file.ObjectKey);

        if (result.IsSuccess)
        {
            var bytes = result.GetValue();
            _logger.LogInformation("Successfully read {ByteCount} bytes from storage", bytes.Length);
        }
        else
        {
            _logger.LogError("Failed to read file: {Error}", result.GetError().Message);
        }

        return result;
    }

    public async Task<Result<byte[]>> DownloadMultipleFilesAsZipAsync(List<Guid> fileIds, string userId)
    {
        var files = new List<CrateFileEntity>();

        foreach (var fileId in fileIds)
        {
            var file = await _context.CrateFiles
                .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

            if (file == null)
                return Result<byte[]>.Failure(new FileNotFoundError($"File {fileId} not found"));

            var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
            if (role == null)
                return Result<byte[]>.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            files.Add(file);
        }

        try
        {
            using var zipStream = new MemoryStream();
            using (var archive =
                   new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var file in files)
                {
                    var fileContentResult =
                        await _storageService.ReadFileAsync(file.CrateId, file.CrateFolderId, file.Name);
                    if (fileContentResult.IsFailure)
                        return Result<byte[]>.Failure(fileContentResult.GetError());

                    var entry = archive.CreateEntry(file.Name);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(fileContentResult.GetValue());
                }
            }

            return Result<byte[]>.Success(zipStream.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception creating zip for user {UserId}", userId);
            return Result<byte[]>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<PaginatedResult<CrateFileResponse>>> GetFilesAsync(FolderContentsParameters parameters)
    {
        try
        {
            var query = _context.CrateFiles
                .Include(f => f.CrateFolder)
                .Include(f => f.UploadedByUser)
                .ApplyFolderContentsFiltering(parameters);

            var pagedFiles = await query.PaginateAsync(parameters.Page, parameters.PageSize);

            var files = new List<CrateFileResponse>();
            foreach (var file in pagedFiles.Items)
            {
                var urlResult = await _storageService.GetFileUrlAsync(file.CrateId, file.CrateFolderId, file.Name);
                var domainFile = file.ToDomain();

                var fileResponse = CrateFileDomainMapper.ToCrateFileResponse(
                    domainFile,
                    domainFile.UploadedByUser != null ? UserMapper.ToUploader(domainFile.UploadedByUser) : null,
                    urlResult.IsSuccess ? urlResult.GetValue() : null
                );

                files.Add(fileResponse);
            }

            var result = new PaginatedResult<CrateFileResponse>
            {
                Items = files,
                TotalCount = pagedFiles.TotalCount,
                Page = pagedFiles.Page,
                PageSize = pagedFiles.PageSize
            };

            return Result<PaginatedResult<CrateFileResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting files for CrateId {CrateId}, UserId {UserId}",
                parameters.CrateId, parameters.UserId);
            return Result<PaginatedResult<CrateFileResponse>>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<List<CrateFile>>> GetFilesInFolderRecursivelyAsync(Guid folderId,
        bool includeDeleted = false)
    {
        try
        {
            var allFiles = new List<CrateFileEntity>();
            var foldersToProcess = new Queue<Guid>();
            foldersToProcess.Enqueue(folderId);

            while (foldersToProcess.Count > 0)
            {
                var currentFolderId = foldersToProcess.Dequeue();

                var query = _context.CrateFiles
                    .Where(f => f.CrateFolderId == currentFolderId);

                // Add IgnoreQueryFilters if we want deleted files
                if (includeDeleted)
                {
                    query = query.IgnoreQueryFilters().Where(f => f.CrateFolderId == currentFolderId);
                }
                else
                {
                    query = query.Where(f => !f.IsDeleted);
                }

                var filesInFolder = await query.ToListAsync();
                allFiles.AddRange(filesInFolder);

                var subfolderQuery = _context.CrateFolders
                    .Where(f => f.ParentFolderId == currentFolderId);

                if (includeDeleted)
                {
                    subfolderQuery = subfolderQuery.IgnoreQueryFilters();
                }
                else
                {
                    subfolderQuery = subfolderQuery.Where(f => !f.IsDeleted);
                }

                var subfolders = await subfolderQuery.Select(f => f.Id).ToListAsync();

                foreach (var subId in subfolders)
                    foldersToProcess.Enqueue(subId);
            }

            var domainFiles = allFiles.Select(f => f.ToDomain()).ToList();
            return Result<List<CrateFile>>.Success(domainFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting files recursively for FolderId {FolderId}", folderId);
            return Result<List<CrateFile>>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result<long>> GetTotalFileSizeInFolderAsync(Guid folderId)
    {
        try
        {
            var totalSize = await _context.CrateFiles
                .Where(f => f.CrateFolderId == folderId && !f.IsDeleted)
                .SumAsync(f => (long?)f.SizeInBytes) ?? 0;

            return Result<long>.Success(totalSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calculating folder size for FolderId {FolderId}", folderId);
            return Result<long>.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> UpdateFileAsync(Guid fileId, UpdateFileRequest request, string userId)
    {
        try
        {
            var fileEntity = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
            if (fileEntity == null)
                return Result.Failure(new FileNotFoundError());

            var role = await _crateRoleService.GetUserRole(fileEntity.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canUpdate = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => fileEntity.UploadedByUserId == userId,
                _ => false
            };

            if (!canUpdate)
                return Result.Failure(new CrateUnauthorizedError("Cannot update this file"));

            if (request.NewName != null && request.NewName != fileEntity.Name)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var storageResult = await _storageService.RenameFileAsync(
                        fileEntity.CrateId,
                        fileEntity.CrateFolderId,
                        fileEntity.Name,
                        request.NewName);

                    if (storageResult.IsFailure)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure(storageResult.GetError());
                    }

                    var domainFile = fileEntity.ToDomain();
                    domainFile.Rename(request.NewName);
                    var updatedEntity = domainFile.ToEntity(fileEntity.CrateId);

                    _context.Entry(fileEntity).CurrentValues.SetValues(updatedEntity);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Exception updating file {FileId} for user {UserId}", fileId, userId);
                    return Result.Failure(new InternalError(ex.Message));
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateFileAsync for FileId {FileId}, UserId {UserId}", fileId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null)
            return Result.Failure(new FileNotFoundError());

        var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
        if (role == null)
            return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        var canDelete = role switch
        {
            CrateRole.Owner => true,
            CrateRole.Manager => true,
            CrateRole.Member => file.UploadedByUserId == userId,
            _ => false
        };

        if (!canDelete)
            return Result.Failure(new CrateUnauthorizedError("Cannot delete this file"));

        return await PermanentlyDeleteFilesAsync(new List<Guid> { fileId }, userId);
    }

    public async Task<Result> SoftDeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null)
            return Result.Failure(new FileNotFoundError());

        var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
        if (role == null)
            return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        var canDelete = role switch
        {
            CrateRole.Owner => true,
            CrateRole.Manager => true,
            CrateRole.Member => file.UploadedByUserId == userId,
            _ => false
        };

        if (!canDelete)
            return Result.Failure(new CrateUnauthorizedError("Cannot delete this file"));

        try
        {
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
            _logger.LogError(ex, "Exception soft deleting file {FileId} for user {UserId}", fileId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null)
            return Result.Failure(new FileNotFoundError());

        var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
        if (role == null)
            return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        var canMove = role switch
        {
            CrateRole.Owner => true,
            CrateRole.Manager => true,
            CrateRole.Member => file.UploadedByUserId == userId,
            _ => false
        };

        if (!canMove)
            return Result.Failure(new CrateUnauthorizedError("Cannot move this file"));

        if (newParentId.HasValue && newParentId.Value != Guid.Empty)
        {
            var folderExists = await _context.CrateFolders
                .AnyAsync(f => f.Id == newParentId.Value && f.CrateId == file.CrateId);
            if (!folderExists)
                return Result.Failure(new NotFoundError(DESTINATION_FOLDER_NOT_FOUND_MESSAGE));
        }
        else
        {
            newParentId = null;
        }

        try
        {
            var storageResult = await _storageService.MoveFileAsync(
                file.CrateId, file.CrateFolderId, newParentId, file.Name);
            if (storageResult.IsFailure)
                return Result.Failure(storageResult.GetError());

            var domainFile = file.ToDomain();
            domainFile.MoveTo(newParentId);

            file.CrateFolderId = domainFile.CrateFolderId;
            file.UpdatedAt = domainFile.UpdatedAt;

            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception moving file {FileId} for user {UserId}", fileId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> RestoreFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null)
            return Result.Failure(new FileNotFoundError());

        var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
        if (role == null)
            return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        var canRestore = role switch
        {
            CrateRole.Owner => true,
            CrateRole.Manager => true,
            CrateRole.Member => file.UploadedByUserId == userId || file.DeletedByUserId == userId,
            _ => false
        };

        if (!canRestore)
            return Result.Failure(new CrateUnauthorizedError("Cannot restore this file"));

        if (file.CrateFolderId.HasValue)
        {
            var parent = await _context.CrateFolders
                .FirstOrDefaultAsync(f => f.Id == file.CrateFolderId.Value);

            if (parent == null || parent.IsDeleted)
            {
                _logger.LogInformation(
                    "Restoring file {FileId} to root because parent folder {FolderId} is {Status}",
                    fileId,
                    file.CrateFolderId,
                    parent == null ? "missing" : "deleted");

                file.CrateFolderId = null;
            }
        }

        try
        {
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
            _logger.LogError(ex, "Exception restoring file {FileId} for user {UserId}", fileId, userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }


    public async Task<Result> SoftDeleteFilesAsync(List<Guid> fileIds, string userId)
    {
        if (!fileIds?.Any() == true)
            return Result.Success();

        var files = await _context.CrateFiles
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync();

        foreach (var file in files)
        {
            var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canDelete = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => file.UploadedByUserId == userId,
                _ => false
            };

            if (!canDelete)
                return Result.Failure(new CrateUnauthorizedError($"Cannot delete file {file.Name}"));
        }

        try
        {
            foreach (var file in files)
            {
                var domainFile = file.ToDomain();
                domainFile.SoftDelete(userId);
                var updatedEntity = domainFile.ToEntity(file.CrateId);
                _context.Entry(file).CurrentValues.SetValues(updatedEntity);
            }

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to soft delete files");
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> PermanentlyDeleteFilesAsync(List<Guid> fileIds, string userId)
    {
        if (!fileIds?.Any() == true)
            return Result.Success();

        var files = await _context.CrateFiles
            .IgnoreQueryFilters()
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync();

        if (files == null || !files.Any())
            return Result.Failure(new FileNotFoundError("No files found to delete"));

        foreach (var file in files)
        {
            if (file == null || string.IsNullOrEmpty(file.Name))
            {
                _logger.LogWarning("Skipping file with null properties: {FileId}", file?.Id);
                continue;
            }

            var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canDelete = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => file.UploadedByUserId == userId || file.DeletedByUserId == userId,
                _ => false
            };

            if (!canDelete)
                return Result.Failure(new CrateUnauthorizedError($"Cannot delete file {file.Name}"));
        }

        try
        {
            var validFiles = files.Where(f => f != null && !string.IsNullOrEmpty(f.Name)).ToList();

            if (!validFiles.Any())
            {
                _logger.LogWarning("No valid files to delete after filtering nulls");
                return Result.Success();
            }

            var fileGroups = validFiles.GroupBy(f => new { f.CrateId, f.CrateFolderId });

            foreach (var group in fileGroups)
            {
                try
                {
                    var storageResult = await _storageService.DeleteFilesAsync(
                        group.Key.CrateId,
                        group.Key.CrateFolderId,
                        group.Select(f => f.Name).Where(name => !string.IsNullOrEmpty(name)));

                    if (!storageResult.IsSuccess)
                    {
                        var errorMsg = storageResult.GetError()?.Message ?? "Unknown storage error";
                        _logger.LogWarning("Failed to delete files from storage: {Error}", errorMsg);
                    }
                }
                catch (Exception ex)
                {
                    // Don't fail the entire operation if storage cleanup fails
                    _logger.LogError(ex,
                        "Exception during storage deletion for crate {CrateId}, continuing with database cleanup",
                        group.Key.CrateId);
                }
            }

            // Always proceed with database cleanup even if storage deletion had issues
            _context.CrateFiles.RemoveRange(validFiles);
            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to permanently delete files");
            return Result.Failure(new InternalError(ex.Message));
        }
    }

    public async Task<Result> MoveFilesAsync(IEnumerable<Guid> fileIds, Guid? newParentId, string userId)
    {
        var fileIdList = fileIds.ToList();
        if (!fileIdList.Any()) return Result.Success();

        var files = await _context.CrateFiles
            .Where(f => fileIdList.Contains(f.Id))
            .ToListAsync();

        foreach (var file in files)
        {
            var role = await _crateRoleService.GetUserRole(file.CrateId, userId);
            if (role == null)
                return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

            var canMove = role switch
            {
                CrateRole.Owner => true,
                CrateRole.Manager => true,
                CrateRole.Member => file.UploadedByUserId == userId,
                _ => false
            };

            if (!canMove)
                return Result.Failure(new CrateUnauthorizedError("Cannot move this file"));
        }

        try
        {
            foreach (var file in files)
            {
                var storageResult = await _storageService.MoveFileAsync(
                    file.CrateId, file.CrateFolderId, newParentId, file.Name);
                if (storageResult.IsFailure)
                    return Result.Failure(storageResult.GetError());

                var domainFile = file.ToDomain();
                domainFile.MoveTo(newParentId);

                file.CrateFolderId = domainFile.CrateFolderId;
                file.UpdatedAt = domainFile.UpdatedAt;
            }

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move files");
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
}