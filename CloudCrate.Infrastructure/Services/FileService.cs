using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class FileService : IFileService
{
    private readonly IAppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FileService(IAppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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
                CategoryId = f.CategoryId
            })
            .ToListAsync();

        return Result<List<FileObjectResponse>>.Success(files);
    }

    public async Task<Result<List<FileObjectResponse>>> GetFilesInFolderAsync(Guid folderId, string userId)
    {
        var files = await _context.FileObjects
            .Where(f => f.FolderId == folderId && f.Folder!.Crate.UserId == userId)
            .Select(f => new FileObjectResponse
            {
                Id = f.Id,
                Name = f.Name,
                MimeType = f.MimeType,
                SizeInBytes = f.SizeInBytes,
                CrateId = f.CrateId,
                FolderId = f.FolderId,
                CategoryId = f.CategoryId
            })
            .ToListAsync();

        return Result<List<FileObjectResponse>>.Success(files);
    }

    public async Task<Result<FileObjectResponse>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == request.CrateId && c.UserId == userId);

        if (crate == null)
            return Result<FileObjectResponse>.Failure(Errors.CrateNotFound);

        if (request.FolderId.HasValue)
        {
            var folder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == request.FolderId && f.Crate.UserId == userId);

            if (folder == null)
                return Result<FileObjectResponse>.Failure(Errors.FolderNotFound);
        }

        using var ms = new MemoryStream();
        await request.Content.CopyToAsync(ms);
        var fileData = ms.ToArray(); // Placeholder, not stored yet

        var file = new FileObject
        {
            Id = Guid.NewGuid(),
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
            CategoryId = file.CategoryId
        };

        return Result<FileObjectResponse>.Success(response);
    }

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result<byte[]>.Failure(Errors.FileNotFound);

        byte[] content = new byte[file.SizeInBytes]; // Placeholder only
        return Result<byte[]>.Success(content);
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result.Failure(Errors.FileNotFound);

        _context.FileObjects.Remove(file);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FileObjectResponse>> GetFileByIdAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result<FileObjectResponse>.Failure(Errors.FileNotFound);

        var response = new FileObjectResponse
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            FolderId = file.FolderId,
            CategoryId = file.CategoryId
        };

        return Result<FileObjectResponse>.Success(response);
    }
}