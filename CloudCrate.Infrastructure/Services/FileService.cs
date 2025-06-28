using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
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

    public async Task<Result<List<Folder>>> GetFoldersAsync(Guid crateId, string userId)
    {
        var folders = await _context.Folders
            .Where(f => f.CrateId == crateId && f.Crate.UserId == userId)
            .ToListAsync();

        return Result<List<Folder>>.Success(folders);
    }

    public async Task<Result<List<FileObject>>> GetFilesInCrateRootAsync(Guid crateId, string userId)
    {
        var files = await _context.FileObjects
            .Where(f => f.CrateId == crateId && f.FolderId == null && f.Crate.UserId == userId)
            .ToListAsync();

        return Result<List<FileObject>>.Success(files);
    }

    public async Task<Result<List<FileObject>>> GetFilesInFolderAsync(Guid folderId, string userId)
    {
        var files = await _context.FileObjects
            .Where(f => f.FolderId == folderId && f.Folder!.Crate.UserId == userId)
            .ToListAsync();

        return Result<List<FileObject>>.Success(files);
    }

    public async Task<Result<FileObject>> UploadFileAsync(FileUploadRequest request, string userId)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == request.CrateId && c.UserId == userId);

        if (crate == null)
            return Result<FileObject>.Failure(Errors.CrateNotFound);

        if (request.FolderId.HasValue)
        {
            var folder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == request.FolderId && f.Crate.UserId == userId);

            if (folder == null)
                return Result<FileObject>.Failure(Errors.FolderNotFound);
        }

        using var ms = new MemoryStream();
        await request.Content.CopyToAsync(ms);
        var fileData = ms.ToArray();

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

        return Result<FileObject>.Success(file);
    }

    public async Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        if (file == null)
            return Result<byte[]>.Failure(Errors.FileNotFound);

        // This is where you'd normally fetch the binary content from blob storage, etc.
        byte[] content = new byte[file.SizeInBytes]; // placeholder only
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

    public async Task<Result<FileObject>> GetFileByIdAsync(Guid fileId, string userId)
    {
        var file = await _context.FileObjects
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Crate.UserId == userId);

        return file != null
            ? Result<FileObject>.Success(file)
            : Result<FileObject>.Failure(Errors.FileNotFound);
    }
}