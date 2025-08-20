using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Extensions;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Entities;
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
        ICratePermissionService cratePermissionService, IUserService userService)
    {
        _context = context;
        _storageService = storageService;
        _cratePermissionService = cratePermissionService;
        _userService = userService;
    }

    public async Task<Result<FileObjectResponse>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        var uploadPermission = await _cratePermissionService.CheckUploadPermissionAsync(request.CrateId, userId);
        if (!uploadPermission.Succeeded)
            return Result<FileObjectResponse>.Failure(uploadPermission.Errors);

        if (request.FolderId.HasValue)
        {
            var folderExists =
                await _context.Folders.AnyAsync(f => f.Id == request.FolderId && f.CrateId == request.CrateId);
            if (!folderExists)
                return Result<FileObjectResponse>.Failure(Errors.Folders.NotFound);
        }

        const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
        if (request.SizeInBytes > MaxFileSize)
            return Result<FileObjectResponse>.Failure(Errors.Files.FileTooLarge);

        if (request.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return Result<FileObjectResponse>.Failure(Errors.Files.VideoNotAllowed);

        var fileId = Guid.NewGuid();

        var saveResult = await _storageService.SaveFileAsync(
            userId,
            request.CrateId,
            request.FolderId,
            request.FileName,
            request.Content
        );

        if (!saveResult.Succeeded)
            return Result<FileObjectResponse>.Failure(saveResult.Errors.First());

        var now = DateTime.UtcNow;

        var file = new FileObject
        {
            Id = fileId,
            Name = request.FileName,
            SizeInBytes = request.SizeInBytes,
            MimeType = request.MimeType,
            ObjectKey = saveResult.Value,
            CrateId = request.CrateId,
            FolderId = request.FolderId,
            UploadedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.FileObjects.Add(file);
        await _context.SaveChangesAsync();

        var uploader = (await _userService.GetUsersByIdsAsync(new[] { userId })).FirstOrDefault();

        var response = new FileObjectResponse
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
        };

        return Result<FileObjectResponse>.Success(response);
    }


    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null)
            return Result<byte[]>.Failure(Errors.Files.NotFound);

        var permissionCheck = await _cratePermissionService.CheckViewPermissionAsync(file.CrateId, userId);
        if (!permissionCheck.Succeeded)
            return Result<byte[]>.Failure(permissionCheck.Errors);

        var fileResult = await _storageService.ReadFileAsync(
            userId,
            file.CrateId,
            file.FolderId,
            file.Name
        );

        if (!fileResult.Succeeded)
            return Result<byte[]>.Failure(fileResult.Errors.First());

        return Result<byte[]>.Success(fileResult.Value);
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null)
            return Result.Failure(Errors.Files.NotFound);

        var deletePermission = await _cratePermissionService.CheckDeletePermissionAsync(file.CrateId, userId);
        if (!deletePermission.Succeeded)
            return Result.Failure(deletePermission.Errors);

        var deleteResult = await _storageService.DeleteFileAsync(
            userId,
            file.CrateId,
            file.FolderId,
            file.Name
        );

        if (!deleteResult.Succeeded)
            return Result.Failure(deleteResult.Errors.First());

        _context.FileObjects.Remove(file);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FileObjectResponse>> GetFileByIdAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null)
            return Result<FileObjectResponse>.Failure(Errors.Files.NotFound);

        var permissionCheck = await _cratePermissionService.CheckViewPermissionAsync(file.CrateId, userId);
        if (!permissionCheck.Succeeded)
            return Result<FileObjectResponse>.Failure(permissionCheck.Errors);

        var urlResult = await _storageService.GetFileUrlAsync(
            userId,
            file.CrateId,
            file.FolderId,
            file.Name
        );

        if (!urlResult.Succeeded)
            return Result<FileObjectResponse>.Failure(urlResult.Errors);

        var uploader = !string.IsNullOrWhiteSpace(file.UploadedByUserId)
            ? await _userService.GetUserByIdAsync(file.UploadedByUserId)
            : null;

        var response = new FileObjectResponse
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
            FileUrl = urlResult.Value,
        };

        return Result<FileObjectResponse>.Success(response);
    }


    public async Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null)
            return Result.Failure(Errors.Files.NotFound);

        var deletePermission = await _cratePermissionService.CheckDeletePermissionAsync(file.CrateId, userId);
        if (!deletePermission.Succeeded)
            return Result.Failure(deletePermission.Errors);

        if (newParentId.HasValue)
        {
            var folderExists =
                await _context.Folders.AnyAsync(f => f.Id == newParentId.Value && f.CrateId == file.CrateId);
            if (!folderExists)
                return Result.Failure(Errors.Folders.NotFound);
        }

        file.FolderId = newParentId;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<PaginatedResult<FileItemDto>> GetFilesAsync(GetFilesParameters parameters)
    {
        // Include the folder (immediate parent only)
        var query = _context.FileObjects
            .Include(f => f.Folder)
            .Where(f => f.CrateId == parameters.CrateId)
            .AsQueryable();

        // Apply search/filtering
        query = ApplyFilters(
            query,
            parameters.SearchTerm,
            parameters.CreatedAfter,
            parameters.CreatedBefore,
            parameters.MinSize,
            parameters.MaxSize
        );

        // Apply folder filter if not searching
        if (string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            query = parameters.FolderId.HasValue
                ? query.Where(f => f.FolderId == parameters.FolderId.Value)
                : query.Where(f => f.FolderId == null); // root
        }

        // Apply ordering
        query = ApplyOrdering(query, parameters.OrderBy, parameters.Ascending);

        // Paginate
        var pagedFiles = await query.PaginateAsync(parameters.Page, parameters.PageSize);

        // Fetch uploaders in bulk
        var uploaderIds = pagedFiles.Items
            .Where(f => !string.IsNullOrEmpty(f.UploadedByUserId))
            .Select(f => f.UploadedByUserId!)
            .Distinct()
            .ToList();

        var uploaders = await _userService.GetUsersByIdsAsync(uploaderIds);
        var uploaderDict = uploaders.ToDictionary(u => u.Id, u => u);

        // Map to DTO
        var fileDtos = pagedFiles.Items.Select(f =>
        {
            uploaderDict.TryGetValue(f.UploadedByUserId ?? string.Empty, out var uploader);

            var fileUrlResult = _storageService.GetFileUrlAsync(
                parameters.UserId,
                f.CrateId,
                f.FolderId,
                f.Name
            ).Result;

            return new FileItemDto
            {
                Id = f.Id,
                Name = f.Name,
                MimeType = f.MimeType,
                SizeInBytes = f.SizeInBytes,
                CrateId = f.CrateId,
                ParentFolderId = f.FolderId,
                ParentFolderName = f.Folder?.Name ?? null,
                UploadedByUserId = f.UploadedByUserId,
                UploadedByDisplayName = uploader?.DisplayName ?? "Unknown",
                UploadedByEmail = uploader?.Email ?? string.Empty,
                UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? string.Empty,
                CreatedAt = f.CreatedAt,
                FileUrl = fileUrlResult.Succeeded ? fileUrlResult.Value : null
            };
        }).ToList();

        return new PaginatedResult<FileItemDto>
        {
            Items = fileDtos,
            TotalCount = pagedFiles.TotalCount,
            Page = pagedFiles.Page,
            PageSize = pagedFiles.PageSize
        };
    }


    private async Task<List<Guid>> GetFolderAndSubfolderIdsFlattenedAsync(Guid folderId)
    {
        var allFolders = await _context.Folders
            .Select(f => new { f.Id, f.ParentFolderId })
            .ToListAsync();

        var result = new List<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(folderId);

        while (stack.Count > 0)
        {
            var currentId = stack.Pop();
            result.Add(currentId);

            var children = allFolders
                .Where(f => f.ParentFolderId == currentId)
                .Select(f => f.Id);

            foreach (var childId in children)
                stack.Push(childId);
        }

        return result;
    }

    private IQueryable<FileObject> ApplyFilters(
        IQueryable<FileObject> query,
        string? searchTerm,
        DateTime? createdAfter,
        DateTime? createdBefore,
        long? minSize,
        long? maxSize)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{searchTerm}%"));

        if (createdAfter.HasValue) query = query.Where(f => f.CreatedAt >= createdAfter.Value);
        if (createdBefore.HasValue) query = query.Where(f => f.CreatedAt <= createdBefore.Value);
        if (minSize.HasValue) query = query.Where(f => f.SizeInBytes >= minSize.Value);
        if (maxSize.HasValue) query = query.Where(f => f.SizeInBytes <= maxSize.Value);

        return query;
    }

    private IQueryable<FileObject> ApplyOrdering(
        IQueryable<FileObject> query,
        FileOrderBy orderBy,
        bool ascending)
    {
        switch (orderBy)
        {
            case FileOrderBy.Name:
                return ascending ? query.OrderBy(f => f.Name) : query.OrderByDescending(f => f.Name);
            case FileOrderBy.SizeInBytes:
                return ascending ? query.OrderBy(f => f.SizeInBytes) : query.OrderByDescending(f => f.SizeInBytes);
            case FileOrderBy.CreatedAt:
            default:
                return ascending ? query.OrderBy(f => f.CreatedAt) : query.OrderByDescending(f => f.CreatedAt);
        }
    }
}