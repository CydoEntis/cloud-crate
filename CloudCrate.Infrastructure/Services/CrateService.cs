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

    public async Task<Result<CrateDto>> CreateCrateAsync(string userId, string crateName)
    {
        var crate = Crate.Create(crateName, userId);
        _context.Crates.Add(crate);
        await _context.SaveChangesAsync();

        var newCrate = new CrateDto
        {
            Id = crate.Id,
            Name = crate.Name,
        };

        return Result<CrateDto>.Success(newCrate);
    }

    public async Task<Result<IEnumerable<CrateDto>>> GetAllCratesAsync(string userId)
    {
        var crates = await _context.Crates
            .Where(c => c.OwnerId == userId)
            .Select(c => new CrateDto
            {
                Id = c.Id,
                Name = c.Name,
            }).ToListAsync();

        return Result<IEnumerable<CrateDto>>.Success(crates);
    }

    public async Task<Result<CrateDto>> RenameCrateAsync(string userId, Guid crateId, string crateName)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            throw new Exception("Crate not found");

        crate.Rename(crateName);

        await _context.SaveChangesAsync();

        var renamedCrate = new CrateDto
        {
            Id = crate.Id,
            Name = crate.Name,
        };

        return Result<CrateDto>.Success(renamedCrate);
    }


    public async Task<Result<string>> UploadFileAsync(string userId, Guid crateId, FileDataDto file)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            return Result<string>.Failure("Crate not found");

        var storedName = $"{Guid.NewGuid()}_{file.FileName}";
        await _fileStorageService.UploadAsync(file.FileStream, storedName);

        var fileObj = new FileObject
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            StoredName = storedName,
            ContentType = file.ContentType,
            Size = file.Size,
            CrateId = file.CrateId
        };

        crate.AddFile(fileObj);
        await _context.SaveChangesAsync();
        return Result<string>.Success("File upload was successful.");
    }

    public async Task<Result<DownloadedFileDto>> DownloadFileAsync(string userId, Guid crateId, Guid fileId)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            return Result<DownloadedFileDto>.Failure("Crate not found");

        var file = crate.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null)
            return Result<DownloadedFileDto>.Failure("File not found");

        var stream = await _fileStorageService.DownloadAsync(file.StoredName);

        var response = new DownloadedFileDto
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType
        };

        return Result<DownloadedFileDto>.Success(response);
    }


    public async Task<Result<IEnumerable<StoredFileDto>>> GetFilesInCrateAsync(Guid crateId, string userId)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            return Result<IEnumerable<StoredFileDto>>.Success(Enumerable.Empty<StoredFileDto>());

        var fileDtos = crate.Files.Select(f => new StoredFileDto
        {
            Id = f.Id,
            FileName = f.FileName,
            StoredName = f.StoredName,
            ContentType = f.ContentType,
            Size = f.Size
        });

        return Result<IEnumerable<StoredFileDto>>.Success(fileDtos);
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