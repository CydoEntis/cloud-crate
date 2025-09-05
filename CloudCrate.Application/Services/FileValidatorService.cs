using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Application.Services;

public sealed record FileTooLargeError(string Message = "The uploaded file exceeds the maximum allowed size") : Error(Message);
public sealed record VideoNotAllowedError(string Message = "Video files are not allowed") : Error(Message);
public sealed record StorageQuotaExceededError(string Message = "Uploading this file would exceed your storage quota") : Error(Message);

public class FileValidatorService : IFileValidatorService
{
    private readonly IAppDbContext _context;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IUserService _userService;

    public FileValidatorService(IAppDbContext context, ICrateRoleService crateRoleService, IUserService userService)
    {
        _context = context;
        _crateRoleService = crateRoleService;
        _userService = userService;
    }

    public async Task<Result> ValidateUploadAsync(FileUploadRequest request, string userId)
    {
        var roleCheck = await _crateRoleService.CanDownload(request.CrateId, userId);
        if (roleCheck.IsFailure)
            return Result.Failure(roleCheck.Error!);

        if (request.FolderId.HasValue)
        {
            bool exists = await _context.CrateFolders
                .AnyAsync(f => f.Id == request.FolderId.Value && f.CrateId == request.CrateId);
            if (!exists)
                return Result.Failure(new NotFoundError("Folder not found"));
        }

        const long MaxFileSize = 10 * 1024 * 1024;
        if (request.SizeInBytes > MaxFileSize)
            return Result.Failure(new FileTooLargeError());

        if (request.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new VideoNotAllowedError());

        var userResult = await _userService.GetUserByIdAsync(userId);
        if (userResult.IsFailure || userResult.Value == null)
            return Result.Failure(new NotFoundError("User not found"));

        var user = userResult.Value;

        if (user.UsedStorageBytes + request.SizeInBytes > user.MaxStorageBytes)
            return Result.Failure(new StorageQuotaExceededError());

        return Result.Success();
    }
}
