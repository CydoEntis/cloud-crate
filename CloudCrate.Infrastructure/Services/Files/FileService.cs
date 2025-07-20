using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Files;

public class FileService : IFileService
{
    private readonly IAppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStorageService _storageService;

    public FileService(
        IAppDbContext context,
        UserManager<ApplicationUser> userManager,
        IStorageService storageService)
    {
        _context = context;
        _userManager = userManager;
        _storageService = storageService;
    }

    public async Task<Result<List<FolderResponse>>> GetFoldersAsync(Guid crateId, string userId)
    {
        var folders = await _context.Folders
            .Where(f => f.CrateId == crateId && f.Crate.UserId == userId)
            .Select(f => new FolderResponse
            {
                Id = f.Id,
                Name = f.Name,
                CrateId = f.CrateId,
                ParentFolderId = f.ParentFolderId
            })
            .ToListAsync();

        return Result<List<FolderResponse>>.Success(folders);
    }

    public async Task<Result<List<FileObjectResponse>>> GetFilesInCrateRootAsync(Guid crateId, string userId)
    {
        var files = await _context.FileObjects
            .Where(f => f.CrateId == crateId && f.FolderId == null && f.Crate.UserId == userId)
            .Select(f => new FileObjectResponse
            {
                Id = f.Id,
                Name = f.Name,
                MimeType = f.MimeType,
                SizeInBytes = f.SizeInBytes,
                CrateId = f.CrateId,
                FolderId = f.FolderId,
            })
            .ToListAsync();

        return Result<List<FileObjectResponse>>.Success(files);
    }

    public async Task<Result<List<FileObjectResponse>>> GetFilesInFolderAsync(Guid crateId, Guid folderId,
        string userId)
    {
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.CrateId == crateId && f.Crate.UserId == userId);

        if (folder == null)
        {
            return Result<List<FileObjectResponse>>.Failure(Errors.Folders.NotFound);
        }

        var files = await _context.FileObjects
            .Where(f => f.FolderId == folderId)
            .Select(f => new FileObjectResponse
            {
                Id = f.Id,
                Name = f.Name,
                MimeType = f.MimeType,
                SizeInBytes = f.SizeInBytes,
                CrateId = f.CrateId,
                FolderId = f.FolderId,
            })
            .ToListAsync();

        return Result<List<FileObjectResponse>>.Success(files);
    }

    public async Task<Result<FileObjectResponse>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == request.CrateId && c.UserId == userId);

        if (crate == null)
            return Result<FileObjectResponse>.Failure(Errors.Crates.NotFound);

        if (request.FolderId.HasValue)
        {
            var folder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == request.FolderId && f.Crate.UserId == userId);

            if (folder == null)
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

        var file = new FileObject
        {
            Id = fileId,
            Name = request.FileName,
            SizeInBytes = request.SizeInBytes,
            MimeType = request.MimeType,
            CrateId = request.CrateId,
            FolderId = request.FolderId,
        };

        _context.FileObjects.Add(file);
        await _context.SaveChangesAsync();

        var response = new FileObjectResponse
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            FolderId = file.FolderId,
        };

        return Result<FileObjectResponse>.Success(response);
    }

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result<byte[]>.Failure(Errors.Files.NotFound);

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
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result.Failure(Errors.Files.NotFound);

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
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result<FileObjectResponse>.Failure(Errors.Files.NotFound);

        var response = new FileObjectResponse
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            FolderId = file.FolderId,
        };

        return Result<FileObjectResponse>.Success(response);
    }

    public async Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId)
    {
        var file = await _context.FileObjects
            .Include(f => f.Crate)
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result.Failure(Errors.Files.NotFound);

        if (newParentId.HasValue)
        {
            var newParentFolder = await _context.Folders
                .Include(f => f.Crate)
                .FirstOrDefaultAsync(f => f.Id == newParentId.Value && f.Crate.UserId == userId);

            if (newParentFolder == null)
                return Result.Failure(Errors.Folders.NotFound);
        }

        file.FolderId = newParentId;
        await _context.SaveChangesAsync();

        return Result.Success();
    }
}