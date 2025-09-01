using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Mappings;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.User.Mappers;
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
        if (!fileResult.Succeeded) return Result<CrateFileResponse>.Failure(fileResult.Errors);

        var fileResponse = await MapFileWithUploaderAsync(fileResult.Value, userId);
        return Result<CrateFileResponse>.Success(fileResponse);
    }

    public async Task<Result<byte[]>> FetchFileBytesAsync(Guid fileId, string userId)
    {
        var fileResult = await FetchAuthorizedFileAsync(fileId, userId);
        if (!fileResult.Succeeded) return Result<byte[]>.Failure(fileResult.Errors);

        var file = fileResult.Value;
        var bytesResult = await _storageService.ReadFileAsync(userId, file.CrateId, file.CrateFolderId, file.Name);

        return bytesResult.Succeeded
            ? Result<byte[]>.Success(bytesResult.Value)
            : Result<byte[]>.Failure(bytesResult.Errors.First());
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

    public async Task<Result<Guid>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        var validationResult = await _fileValidatorService.ValidateUploadAsync(request, userId);
        if (!validationResult.Succeeded)
            return Result<Guid>.Failure(validationResult.Errors);

        var saveResult = await _storageService.SaveFileAsync(userId, request);
        if (!saveResult.Succeeded)
            return Result<Guid>.Failure(saveResult.Errors.First());

        var crateFile = CrateFile.Create(
            request.FileName,
            request.SizeInBytes,
            request.MimeType,
            request.CrateId,
            userId,
            request.FolderId
        );
        crateFile.ObjectKey = saveResult.Value;

        _context.CrateFiles.Add(crateFile);
        await _context.SaveChangesAsync();

        return Result<Guid>.Success(crateFile.Id);
    }

    #endregion

    #region Delete / Restore / Move Files

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result<byte[]>.Failure(Errors.Files.NotFound);

        var permissionCheck = await _crateRoleService.CanDownload(file.CrateId, userId);
        if (!permissionCheck.Succeeded) return Result<byte[]>.Failure(permissionCheck.Errors);

        var fileResult = await _storageService.ReadFileAsync(userId, file.CrateId, file.CrateFolderId, file.Name);
        if (!fileResult.Succeeded) return Result<byte[]>.Failure(fileResult.Errors.First());

        return Result<byte[]>.Success(fileResult.Value);
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(Errors.Files.NotFound);

        var deletePermission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (!deletePermission.Succeeded) return Result.Failure(deletePermission.Errors);

        var deleteResult = await _storageService.DeleteFileAsync(userId, file.CrateId, file.CrateFolderId, file.Name);
        if (!deleteResult.Succeeded) return Result.Failure(deleteResult.Errors.First());

        _context.CrateFiles.Remove(file);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> SoftDeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(Errors.Files.NotFound);

        var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

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
            if (!result.Succeeded) return result;
        }

        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteFilesAsync(List<Guid> fileIds, string userId)
    {
        foreach (var fileId in fileIds)
        {
            var result = await DeleteFileAsync(fileId, userId);
            if (!result.Succeeded) return result;
        }

        return Result.Success();
    }

    public async Task<Result> DeleteFilesInFolderRecursivelyAsync(Guid folderId, string userId)
    {
        var files = await _context.CrateFiles.Where(f => f.CrateFolderId == folderId).ToListAsync();
        foreach (var file in files)
        {
            var result = await DeleteFileAsync(file.Id, userId);
            if (!result.Succeeded) return result;
        }

        var subfolders = await _context.Folders.Where(f => f.ParentFolderId == folderId).ToListAsync();
        foreach (var sub in subfolders)
        {
            var result = await DeleteFilesInFolderRecursivelyAsync(sub.Id, userId);
            if (!result.Succeeded) return result;
        }

        return Result.Success();
    }

    public async Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(Errors.Files.NotFound);

        var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        if (newParentId.HasValue && newParentId.Value == Guid.Empty) newParentId = null;

        if (newParentId.HasValue)
        {
            var folderExists =
                await _context.Folders.AnyAsync(f => f.Id == newParentId.Value && f.CrateId == file.CrateId);
            if (!folderExists) return Result.Failure(Errors.Folders.NotFound);
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
            if (!result.Succeeded) return result;
        }

        return Result.Success();
    }

    public async Task<Result> RestoreFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(Errors.Files.NotFound);

        var permission = await _crateRoleService.CanManageCrate(file.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        if (file.CrateFolderId.HasValue)
        {
            var parent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == file.CrateFolderId.Value);
            if (parent == null) return Result.Failure(Errors.Folders.NotFound);
            if (parent.IsDeleted)
                return Result.Failure(Errors.Folders.InvalidMove with
                {
                    Message = "Parent folder is deleted. Restore the parent first or move to root."
                });
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
            if (!result.Succeeded) return result;
        }

        return Result.Success();
    }

    #endregion

    #region Helpers

    private async Task<Result<CrateFile>> FetchAuthorizedFileAsync(Guid fileId, string userId)
    {
        var file = await _context.CrateFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        if (file == null) return Result<CrateFile>.Failure(Errors.Files.NotFound);

        var permission = await _crateRoleService.CanView(file.CrateId, userId);
        if (!permission.Succeeded) return Result<CrateFile>.Failure(permission.Errors);

        return Result<CrateFile>.Success(file);
    }

    private async Task<CrateFileResponse> MapFileWithUploaderAsync(CrateFile file, string currentUserId)
    {
        var user = await _userService.GetUserByIdAsync(file.UploaderId);

        var urlResult =
            await _storageService.GetFileUrlAsync(currentUserId, file.CrateId, file.CrateFolderId, file.Name);
        var response = FileMapper.ToCrateFileResponse(file, UserMapper.ToUploader(user),
            urlResult.Succeeded ? urlResult.Value : null);

        return response;
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