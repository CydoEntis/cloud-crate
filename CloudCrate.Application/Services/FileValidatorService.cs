using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Application.Services;

public sealed record FileTooLargeError(string Message = "The uploaded file exceeds the maximum allowed size") : Error(Message);
public sealed record VideoNotAllowedError(string Message = "Video files are not allowed") : Error(Message);

public class FileValidatorService : IFileValidatorService
{
    private readonly IAppDbContext _context;
    private readonly ICrateRoleService _crateRoleService;

    public FileValidatorService(IAppDbContext context, ICrateRoleService crateRoleService)
    {
        _context = context;
        _crateRoleService = crateRoleService;
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

        // Validate file size (max 10 MB)
        const long MaxFileSize = 10 * 1024 * 1024;
        if (request.SizeInBytes > MaxFileSize)
            return Result.Failure(new FileTooLargeError());

        // Validate MIME type
        if (request.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new VideoNotAllowedError());

        return Result.Success();
    }
}