using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services;

public class CrateService : ICrateService
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public CrateService(AppDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<Result<CrateResponse>> CreateCrateAsync(string userId, string crateName)
    {
        var crate = Crate.Create(crateName, userId);
        _context.Crates.Add(crate);
        await _context.SaveChangesAsync();

        var dto = new CrateResponse
        {
            Id = crate.Id,
            Name = crate.Name,
        };

        return Result<CrateResponse>.Success(dto);
    }

    public async Task<Result<IEnumerable<CrateResponse>>> GetAllCratesAsync(string userId)
    {
        var crates = await _context.Crates
            .Where(c => c.OwnerId == userId)
            .Select(c => new CrateResponse
            {
                Id = c.Id,
                Name = c.Name,
            }).ToListAsync();

        return Result<IEnumerable<CrateResponse>>.Success(crates);
    }

    public async Task<Result<CrateResponse>> RenameCrateAsync(string userId, RenameCrateRequest request)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == request.CrateId && c.OwnerId == userId);

        if (crate == null)
            throw new Exception("Crate not found");

        crate.Rename(request.NewName);

        await _context.SaveChangesAsync();

        var response = new CrateResponse
        {
            Id = crate.Id,
            Name = crate.Name,
        };

        return Result<CrateResponse>.Success(response);
    }

    public async Task<Result<string>> AddFileToCrateAsync(string userId, AddFileToCrateRequest request)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == request.CrateId && c.OwnerId == userId);

        if (crate == null)
            return Result<string>.Failure("Crate not found");

        crate.AddFile(request.File);

        await _context.SaveChangesAsync();
        return Result<string>.Success("File added file to crate.");
    }

    public async Task<Result<string>> UploadFileAsync(string userId, FileDataRequest dataRequest)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == dataRequest.CrateId && c.OwnerId == userId);

        if (crate == null)
            return Result<string>.Failure("Crate not found");

        var storedName = $"{Guid.NewGuid()}_{dataRequest.FileName}";
        await _fileStorageService.UploadAsync(dataRequest.FileStream, storedName);

        var fileObj = new FileObject
        {
            Id = Guid.NewGuid(),
            FileName = dataRequest.FileName,
            StoredName = storedName,
            ContentType = dataRequest.ContentType,
            Size = dataRequest.Size,
            CrateId = dataRequest.CrateId
        };

        crate.AddFile(fileObj);
        await _context.SaveChangesAsync();
        return Result<string>.Success("File upload was successful.");
    }

    public async Task<Result<DownloadFileResponse>> DownloadFileAsync(string userId, DownloadFileRequest request)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == request.CrateId && c.OwnerId == userId);

        if (crate == null)
            return Result<DownloadFileResponse>.Failure("Crate not found");

        var file = crate.Files.FirstOrDefault(f => f.Id == request.FileId);
        if (file == null)
            return Result<DownloadFileResponse>.Failure("File not found");

        var stream = await _fileStorageService.DownloadAsync(file.StoredName);

        var response = new DownloadFileResponse
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType
        };

        return Result<DownloadFileResponse>.Success(response);
    }


    public async Task<Result<IEnumerable<FileObjectResponse>>> GetFilesInCrateAsync(Guid crateId, string userId)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            return Result<IEnumerable<FileObjectResponse>>.Success(Enumerable.Empty<FileObjectResponse>());

        var fileDtos = crate.Files.Select(f => new FileObjectResponse
        {
            Id = f.Id,
            FileName = f.FileName,
            StoredName = f.StoredName,
            ContentType = f.ContentType,
            Size = f.Size
        });

        return Result<IEnumerable<FileObjectResponse>>.Success(fileDtos);
    }

    public async Task<Result> DeleteFileAsync(Guid crateId, string userId, Guid fileId)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null) return Result.Failure("Crate not found");

        var file = crate.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return Result.Failure("File not found");

        _context.FileObjects.Remove(file);
        await _fileStorageService.DeleteAsync(file.StoredName);
        await _context.SaveChangesAsync();

        return Result.Success();
    }
}