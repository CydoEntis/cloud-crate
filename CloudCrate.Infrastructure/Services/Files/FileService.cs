using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Mappings;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Files;

public class FileService : IFileService
{
    private readonly IAppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ICratePermissionService _cratePermissionService;
    private readonly IUserService _userService;

    public FileService(
        IAppDbContext context,
        IStorageService storageService,
        ICratePermissionService cratePermissionService,
        IUserService userService)
    {
        _context = context;
        _storageService = storageService;
        _cratePermissionService = cratePermissionService;
        _userService = userService;
    }

    #region File Operations

    public async Task<Result<FileObjectResponse>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        var uploadPermission = await _cratePermissionService.CheckUploadPermissionAsync(request.CrateId, userId);
        if (!uploadPermission.Succeeded)
            return Result<FileObjectResponse>.Failure(uploadPermission.Errors);

        if (request.FolderId.HasValue)
        {
            bool folderExists =
                await _context.Folders.AnyAsync(f => f.Id == request.FolderId && f.CrateId == request.CrateId);
            if (!folderExists) return Result<FileObjectResponse>.Failure(Errors.Folders.NotFound);
        }

        const long MaxFileSize = 10 * 1024 * 1024;
        if (request.SizeInBytes > MaxFileSize)
            return Result<FileObjectResponse>.Failure(Errors.Files.FileTooLarge);

        if (request.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return Result<FileObjectResponse>.Failure(Errors.Files.VideoNotAllowed);

        var saveResult = await _storageService.SaveFileAsync(userId, request.CrateId, request.FolderId,
            request.FileName, request.Content);
        if (!saveResult.Succeeded) return Result<FileObjectResponse>.Failure(saveResult.Errors.First());

        var file = new FileObject
        {
            Id = Guid.NewGuid(),
            Name = request.FileName,
            SizeInBytes = request.SizeInBytes,
            MimeType = request.MimeType,
            ObjectKey = saveResult.Value,
            CrateId = request.CrateId,
            FolderId = request.FolderId,
            UploadedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.FileObjects.Add(file);
        await _context.SaveChangesAsync();

        var uploader = (await _userService.GetUsersByIdsAsync(new[] { userId })).FirstOrDefault();

        return Result<FileObjectResponse>.Success(new FileObjectResponse
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            FolderId = file.FolderId,
            UploadedByUserId = file.UploadedByUserId,
            UploadedByDisplayName = uploader?.DisplayName ?? "Unknown",
            UploadedByEmail = uploader?.Email ?? string.Empty,
            UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? string.Empty,
            CreatedAt = file.CreatedAt
        });
    }

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result<byte[]>.Failure(Errors.Files.NotFound);

        var permissionCheck = await _cratePermissionService.CheckViewPermissionAsync(file.CrateId, userId);
        if (!permissionCheck.Succeeded) return Result<byte[]>.Failure(permissionCheck.Errors);

        var fileResult = await _storageService.ReadFileAsync(userId, file.CrateId, file.FolderId, file.Name);
        if (!fileResult.Succeeded) return Result<byte[]>.Failure(fileResult.Errors.First());

        return Result<byte[]>.Success(fileResult.Value);
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(Errors.Files.NotFound);

        var deletePermission = await _cratePermissionService.CheckDeletePermissionAsync(file.CrateId, userId);
        if (!deletePermission.Succeeded) return Result.Failure(deletePermission.Errors);

        var deleteResult = await _storageService.DeleteFileAsync(userId, file.CrateId, file.FolderId, file.Name);
        if (!deleteResult.Succeeded) return Result.Failure(deleteResult.Errors.First());

        _context.FileObjects.Remove(file);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FileObjectResponse>> GetFileByIdAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result<FileObjectResponse>.Failure(Errors.Files.NotFound);

        var permissionCheck = await _cratePermissionService.CheckViewPermissionAsync(file.CrateId, userId);
        if (!permissionCheck.Succeeded) return Result<FileObjectResponse>.Failure(permissionCheck.Errors);

        var urlResult = await _storageService.GetFileUrlAsync(userId, file.CrateId, file.FolderId, file.Name);
        if (!urlResult.Succeeded) return Result<FileObjectResponse>.Failure(urlResult.Errors);

        var uploader = !string.IsNullOrWhiteSpace(file.UploadedByUserId)
            ? await _userService.GetUserByIdAsync(file.UploadedByUserId)
            : null;

        return Result<FileObjectResponse>.Success(new FileObjectResponse
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            FolderId = file.FolderId,
            UploadedByUserId = file.UploadedByUserId ?? string.Empty,
            UploadedByDisplayName = uploader?.DisplayName ?? string.Empty,
            UploadedByEmail = uploader?.Email ?? string.Empty,
            UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? string.Empty,
            CreatedAt = file.CreatedAt,
            FileUrl = urlResult.Value
        });
    }

    public async Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId)
    {
        var file = await _context.FileObjects.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(Errors.Files.NotFound);

        var deletePermission = await _cratePermissionService.CheckDeletePermissionAsync(file.CrateId, userId);
        if (!deletePermission.Succeeded) return Result.Failure(deletePermission.Errors);

        if (newParentId.HasValue && newParentId.Value == Guid.Empty) newParentId = null;

        if (newParentId.HasValue)
        {
            var folderExists =
                await _context.Folders.AnyAsync(f => f.Id == newParentId.Value && f.CrateId == file.CrateId);
            if (!folderExists) return Result.Failure(Errors.Folders.NotFound);
        }

        file.FolderId = newParentId;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    #endregion

    #region Bulk File Operations

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

    public async Task<Result> MoveFilesAsync(IEnumerable<Guid> fileIds, Guid? newParentId, string userId)
    {
        foreach (var fileId in fileIds)
        {
            var result = await MoveFileAsync(fileId, newParentId, userId);
            if (!result.Succeeded) return result;
        }

        return Result.Success();
    }

    #endregion

    #region File Queries & Folder Helpers

    public async Task<PaginatedResult<FileItemDto>> GetFilesAsync(GetFilesParameters parameters)
    {
        var query = _context.FileObjects.Include(f => f.Folder).Where(f => f.CrateId == parameters.CrateId);

        query = ApplyFilters(query, parameters.SearchTerm, parameters.CreatedAfter, parameters.CreatedBefore,
            parameters.MinSize, parameters.MaxSize);

        if (string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            query = parameters.FolderId.HasValue
                ? query.Where(f => f.FolderId == parameters.FolderId)
                : query.Where(f => f.FolderId == null);
        }

        query = ApplyOrdering(query, parameters.OrderBy, parameters.Ascending);

        var pagedFiles = await query.PaginateAsync(parameters.Page, parameters.PageSize);

        var uploaderIds = pagedFiles.Items.Where(f => !string.IsNullOrEmpty(f.UploadedByUserId))
            .Select(f => f.UploadedByUserId!).Distinct().ToList();
        var uploaders = await _userService.GetUsersByIdsAsync(uploaderIds);
        var uploaderDict = uploaders.ToDictionary(u => u.Id, u => u);

        var fileDtos = new List<FileItemDto>();
        foreach (var f in pagedFiles.Items)
        {
            uploaderDict.TryGetValue(f.UploadedByUserId ?? string.Empty, out var uploader);

            var fileUrlResult = await _storageService.GetFileUrlAsync(parameters.UserId, f.CrateId, f.FolderId, f.Name);

            fileDtos.Add(new FileItemDto
            {
                Id = f.Id,
                Name = f.Name,
                MimeType = f.MimeType,
                SizeInBytes = f.SizeInBytes,
                CrateId = f.CrateId,
                ParentFolderId = f.FolderId,
                ParentFolderName = f.Folder?.Name,
                UploadedByUserId = f.UploadedByUserId,
                UploadedByDisplayName = uploader?.DisplayName ?? "Unknown",
                UploadedByEmail = uploader?.Email ?? string.Empty,
                UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? string.Empty,
                CreatedAt = f.CreatedAt,
                FileUrl = fileUrlResult.Succeeded ? fileUrlResult.Value : null
            });
        }

        return new PaginatedResult<FileItemDto>
        {
            Items = fileDtos,
            TotalCount = pagedFiles.TotalCount,
            Page = pagedFiles.Page,
            PageSize = pagedFiles.PageSize
        };
    }

    private IQueryable<FileObject> ApplyFilters(IQueryable<FileObject> query, string? searchTerm,
        DateTime? createdAfter, DateTime? createdBefore, long? minSize, long? maxSize)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{searchTerm}%"));
        if (createdAfter.HasValue) query = query.Where(f => f.CreatedAt >= createdAfter.Value);
        if (createdBefore.HasValue) query = query.Where(f => f.CreatedAt <= createdBefore.Value);
        if (minSize.HasValue) query = query.Where(f => f.SizeInBytes >= minSize.Value);
        if (maxSize.HasValue) query = query.Where(f => f.SizeInBytes <= maxSize.Value);

        return query;
    }

    private IQueryable<FileObject> ApplyOrdering(IQueryable<FileObject> query, FileOrderBy orderBy, bool ascending)
    {
        return orderBy switch
        {
            FileOrderBy.Name => ascending ? query.OrderBy(f => f.Name) : query.OrderByDescending(f => f.Name),
            FileOrderBy.SizeInBytes => ascending
                ? query.OrderBy(f => f.SizeInBytes)
                : query.OrderByDescending(f => f.SizeInBytes),
            FileOrderBy.CreatedAt => ascending
                ? query.OrderBy(f => f.CreatedAt)
                : query.OrderByDescending(f => f.CreatedAt),
            _ => query.OrderBy(f => f.Name)
        };
    }

    public async Task<List<FileObject>> GetFilesInFolderRecursivelyAsync(Guid folderId)
    {
        var files = await _context.FileObjects.Where(f => f.FolderId == folderId).ToListAsync();
        var subfolders = await _context.Folders.Where(f => f.ParentFolderId == folderId).ToListAsync();

        foreach (var sub in subfolders)
        {
            var subFiles = await GetFilesInFolderRecursivelyAsync(sub.Id);
            files.AddRange(subFiles);
        }

        return files;
    }

    public async Task<Result<byte[]>> GetFileBytesAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result<byte[]>.Failure(Errors.Files.NotFound);

        var permission = await _cratePermissionService.CheckViewPermissionAsync(file.CrateId, userId);
        if (!permission.Succeeded) return Result<byte[]>.Failure(permission.Errors);

        var fileResult = await _storageService.ReadFileAsync(userId, file.CrateId, file.FolderId, file.Name);
        if (!fileResult.Succeeded) return Result<byte[]>.Failure(fileResult.Errors.First());

        return Result<byte[]>.Success(fileResult.Value);
    }

    public async Task<long> GetFolderFilesSizeAsync(Guid folderId)
    {
        return await _context.FileObjects
            .Where(f => f.FolderId == folderId)
            .SumAsync(f => (long?)f.SizeInBytes) ?? 0;
    }

    public async Task<List<FolderOrFileItem>> GetFilesForFolderContentsAsync(FolderQueryParameters parameters,
        bool searchMode, string? searchTerm = null)
    {
        var fileDtos = await GetFilesAsync(new GetFilesParameters
        {
            CrateId = parameters.CrateId,
            FolderId = searchMode ? null : parameters.ParentFolderId,
            SearchTerm = searchMode ? searchTerm : null,
            OrderBy = parameters.SortBy == FolderSortBy.CreatedAt ? FileOrderBy.CreatedAt : FileOrderBy.Name,
            Ascending = parameters.OrderBy != OrderBy.Desc,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            UserId = parameters.UserId
        });

        var items = (fileDtos.Items ?? new List<FileItemDto>())
            .Select(FolderItemMapper.MapFile)
            .ToList();

        foreach (var item in items.Where(i => i.Type == FolderItemType.Folder))
        {
            item.SizeInBytes = await GetFolderFilesSizeRecursiveAsync(item.Id);
        }

        return items;
    }

    #endregion

    #region Soft Delete & Folder Operations

    public async Task<Result> SoftDeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return Result.Failure(Errors.Files.NotFound);

        var permission = await _cratePermissionService.CheckDeletePermissionAsync(file.CrateId, userId);
        if (!permission.Succeeded) return Result.Failure(permission.Errors);

        file.IsDeleted = true;
        file.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<long> GetFolderFilesSizeRecursiveAsync(Guid folderId)
    {
        long totalSize = await _context.FileObjects
            .Where(f => f.FolderId == folderId && !f.IsDeleted)
            .SumAsync(f => (long?)f.SizeInBytes) ?? 0;

        var subfolders = await _context.Folders
            .Where(f => f.ParentFolderId == folderId && !f.IsDeleted)
            .ToListAsync();

        foreach (var sub in subfolders)
        {
            totalSize += await GetFolderFilesSizeRecursiveAsync(sub.Id);
        }

        return totalSize;
    }

    public async Task<Result> DeleteFilesInFolderRecursivelyAsync(Guid folderId, string userId)
    {
        var files = await _context.FileObjects.Where(f => f.FolderId == folderId).ToListAsync();
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

    #endregion
}