using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Domain.Entities;
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

        var response = new FileObjectResponse
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            FolderId = file.FolderId,
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
}