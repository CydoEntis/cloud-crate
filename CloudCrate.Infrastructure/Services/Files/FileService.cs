using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Mappers;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.User.Mappers;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public class FileService : IFileService
{
    private readonly IAppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IFileValidatorService _fileValidatorService;
    private readonly IUserService _userService;

    public FileService(
        IAppDbContext context,
        IStorageService storageService,
        ICrateRoleService crateRoleService,
        IFileValidatorService fileValidatorService,
        IUserService userService)
    {
        _context = context;
        _storageService = storageService;
        _crateRoleService = crateRoleService;
        _fileValidatorService = fileValidatorService;
        _userService = userService;
    }

    #region Fetch Files

    public async Task<Result<CrateFileResponse>> FetchFileResponseAsync(Guid fileId, string userId)
    {
        var fileResult = await FetchAuthorizedFileAsync(fileId, userId);
        if (fileResult.IsFailure) return Result<CrateFileResponse>.Failure(fileResult.Error!);

        var fileResponse = await MapFileWithUploaderAsync(fileResult.Value!, userId);
        return Result<CrateFileResponse>.Success(fileResponse);
    }

    public async Task<Result<byte[]>> FetchFileBytesAsync(Guid fileId, string userId)
    {
        var fileResult = await FetchAuthorizedFileAsync(fileId, userId);
        if (fileResult.IsFailure) return Result<byte[]>.Failure(fileResult.Error!);

        var file = fileResult.Value!;
        var bytesResult = await _storageService.ReadFileAsync(userId, file.CrateId, file.CrateFolderId, file.Name);

        return bytesResult.IsSuccess
            ? Result<byte[]>.Success(bytesResult.Value!)
            : Result<byte[]>.Failure(bytesResult.Error!);
    }

    public async Task<PaginatedResult<CrateFileResponse>> FetchFilesAsync(FolderContentsParameters parameters)
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

    public async Task<List<CrateFile>> FetchFilesInFolderRecursivelyAsync(Guid folderId)
    {
        var allFiles = new List<CrateFile>();
        var foldersToProcess = new Queue<Guid>();
        foldersToProcess.Enqueue(folderId);

        while (foldersToProcess.Count > 0)
        {
            var currentFolderId = foldersToProcess.Dequeue();

            var filesInFolder = await _context.CrateFiles
                .Where(f => f.CrateFolderId == currentFolderId && !f.IsDeleted)
                .ToListAsync();

            allFiles.AddRange(filesInFolder);

            var subfolders = await _context.Folders
                .Where(f => f.ParentFolderId == currentFolderId && !f.IsDeleted)
                .Select(f => f.Id)
                .ToListAsync();

            foreach (var subId in subfolders)
                foldersToProcess.Enqueue(subId);
        }

        return allFiles;
    }

    public async Task<long> FetchTotalFileSizeInFolderAsync(Guid folderId)
    {
        return await _context.CrateFiles
            .Where(f => f.CrateFolderId == folderId && !f.IsDeleted)
            .SumAsync(f => (long?)f.SizeInBytes) ?? 0;
    }

    #endregion

    #region Upload Files

    public async Task<Result<List<Guid>>> UploadFilesAsync(MultiFileUploadRequest request, string userId)
    {
        if (request.Files == null || !request.Files.Any())
            return Result<List<Guid>>.Failure(Error.Validation("No files provided.", "Files"));

        var storageResults = new List<Result<string>>();

        // Upload all files to storage first
        foreach (var fileReq in request.Files)
        {
            var result = await _storageService.SaveFileAsync(userId, fileReq);
            if (result.IsFailure)
            {
                // Rollback previous uploads
                foreach (var r in storageResults.Where(r => r.IsSuccess))
                {
                    var uploadedIndex = storageResults.IndexOf(r);
                    var uploadedFile = request.Files[uploadedIndex];
                    await _storageService.DeleteFileAsync(userId, uploadedFile.CrateId, uploadedFile.FolderId, uploadedFile.FileName);
                }
                return Result<List<Guid>>.Failure(result.Error!);
            }

            storageResults.Add(result);
        }

        // Now create DB entries
        var fileEntities = request.Files.Select((req, i) =>
        {
            var file = CrateFile.Create(req.FileName, req.SizeInBytes, req.MimeType, req.CrateId, userId, req.FolderId);
            file.ObjectKey = storageResults[i].Value;
            return file;
        }).ToList();

        _context.CrateFiles.AddRange(fileEntities);

        var totalSize = request.Files.Sum(f => f.SizeInBytes);
        var storageIncrementResult = await _userService.IncrementUsedStorageAsync(userId, totalSize);
        if (storageIncrementResult.IsFailure)
            return Result<List<Guid>>.Failure(storageIncrementResult.Error!);

        await _context.SaveChangesAsync();

        return Result<List<Guid>>.Success(fileEntities.Select(f => f.Id).ToList());
    }

    public async Task<Result<Guid>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        var validationResult = await _fileValidatorService.ValidateUploadAsync(request, userId);
        if (validationResult.IsFailure)
            return Result<Guid>.Failure(validationResult.Error!);

        var saveResult = await _storageService.SaveFileAsync(userId, request);
        if (saveResult.IsFailure)
            return Result<Guid>.Failure(saveResult.Error!);

        var crateFile = CrateFile.Create(
            request.FileName,
            request.SizeInBytes,
            request.MimeType,
            request.CrateId,
            userId,
            request.FolderId
        );
        crateFile.ObjectKey = saveResult.Value!;

        _context.CrateFiles.Add(crateFile);

        var storageResult = await _userService.IncrementUsedStorageAsync(userId, request.SizeInBytes);
        if (storageResult.IsFailure)
            return Result<Guid>.Failure(storageResult.Error!);

        await _context.SaveChangesAsync();

        return Result<Guid>.Success(crateFile.Id);
    }

    #endregion

    #region Delete / Restore / Move Files

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result<byte[]>.Failure(new FileNotFoundError());

        var permissionCheck = await _crateRoleService.CanDownload(file.CrateId, userId);
        if (permissionCheck.IsFailure) return Result<byte[]>.Failure(permissionCheck.Error!);

        var fileResult = await _storageService.ReadFileAsync(userId, file.CrateId, file.CrateFolderId, file.Name);
        if (fileResult.IsFailure) return Result<byte[]>.Failure(fileResult.Error!);

        return Result<byte[]>.Success(fileResult.Value!);
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(new FileNotFoundError());

        var deletePermission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (deletePermission.IsFailure) return Result.Failure(deletePermission.Error!);

        var deleteResult = await _storageService.DeleteFileAsync(userId, file.CrateId, file.CrateFolderId, file.Name);
        if (deleteResult.IsFailure) return Result.Failure(deleteResult.Error!);

        var storageResult = await _userService.DecrementUsedStorageAsync(userId, file.SizeInBytes);
        if (storageResult.IsFailure) return Result.Failure(storageResult.Error!);

        _context.CrateFiles.Remove(file);
        await _context.SaveChangesAsync();

        return Result.Success();
    }


    public async Task<Result> SoftDeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(new FileNotFoundError());

        var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (permission.IsFailure) return Result.Failure(permission.Error!);

        file.IsDeleted = true;
        file.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
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
            var result = await DeleteFileAsync(fileId, userId);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    public async Task<Result> DeleteFilesInFolderRecursivelyAsync(Guid folderId, string userId)
    {
        var files = await _context.CrateFiles.Where(f => f.CrateFolderId == folderId).ToListAsync();
        foreach (var file in files)
        {
            var result = await DeleteFileAsync(file.Id, userId);
            if (result.IsFailure) return result;
        }

        var subfolders = await _context.Folders.Where(f => f.ParentFolderId == folderId).ToListAsync();
        foreach (var sub in subfolders)
        {
            var result = await DeleteFilesInFolderRecursivelyAsync(sub.Id, userId);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    public async Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(new FileNotFoundError());

        var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (permission.IsFailure) return Result.Failure(permission.Error!);

        if (newParentId.HasValue && newParentId.Value == Guid.Empty) newParentId = null;

        if (newParentId.HasValue)
        {
            var folderExists =
                await _context.Folders.AnyAsync(f => f.Id == newParentId.Value && f.CrateId == file.CrateId);
            if (!folderExists) return Result.Failure(new NotFoundError("Destination folder not found"));
        }

        file.CrateFolderId = newParentId;
        await _context.SaveChangesAsync();

        return Result.Success();
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
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(new FileNotFoundError());

        var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (permission.IsFailure) return Result.Failure(permission.Error!);

        if (file.CrateFolderId.HasValue)
        {
            var parent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == file.CrateFolderId.Value);
            if (parent == null) return Result.Failure(new NotFoundError("Parent folder not found"));
            if (parent.IsDeleted)
                return Result.Failure(new FileError("Parent folder is deleted. Restore parent or move to root."));
        }

        file.IsDeleted = false;
        file.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success();
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

    private async Task<Result<CrateFile>> FetchAuthorizedFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null) return Result<CrateFile>.Failure(new FileNotFoundError());

        var permission = await _crateRoleService.CanView(file.CrateId, userId);
        if (permission.IsFailure) return Result<CrateFile>.Failure(permission.Error!);

        return Result<CrateFile>.Success(file);
    }

    private async Task<CrateFileResponse> MapFileWithUploaderAsync(CrateFile file, string currentUserId)
    {
        var userResult = await _userService.GetUserByIdAsync(file.UploaderId);
        UserResponse? user = null;
        if (userResult.IsSuccess && userResult.Value != null)
        {
            user = userResult.Value;
        }

        var urlResult =
            await _storageService.GetFileUrlAsync(currentUserId, file.CrateId, file.CrateFolderId, file.Name);

        return CrateFileMapper.ToCrateFileResponse(
            file,
            user != null ? UserMapper.ToUploader(user) : null,
            urlResult.IsSuccess ? urlResult.Value : null
        );
    }

    private IQueryable<CrateFile> BuildFileQuery(FolderContentsParameters parameters)
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

    private IQueryable<CrateFile> ApplyFilters(IQueryable<CrateFile> query, long? minSize, long? maxSize,
        DateTime? createdAfter, DateTime? createdBefore)
    {
        if (minSize.HasValue) query = query.Where(f => f.SizeInBytes >= minSize.Value);
        if (maxSize.HasValue) query = query.Where(f => f.SizeInBytes <= maxSize.Value);
        if (createdAfter.HasValue) query = query.Where(f => f.CreatedAt >= createdAfter.Value);
        if (createdBefore.HasValue) query = query.Where(f => f.CreatedAt <= createdBefore.Value);
        return query;
    }

    private IQueryable<CrateFile> ApplyOrdering(IQueryable<CrateFile> query, OrderBy orderBy, bool ascending)
    {
        return orderBy switch
        {
            OrderBy.Name => ascending ? query.OrderBy(f => f.Name) : query.OrderByDescending(f => f.Name),
            OrderBy.SizeInBytes => ascending
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