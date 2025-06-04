using CloudCrate.Application.Common.Interfaces;
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

    public async Task<CrateDto> CreateCrateAsync(string userId, string crateName)
    {
        var crate = Crate.Create(crateName, userId);

        _context.Crates.Add(crate);
        await _context.SaveChangesAsync();

        return new CrateDto
        {
            Id = crate.Id,
            Name = crate.Name,
        };
    }

    public async Task<IEnumerable<CrateDto>> GetAllCratesAsync(string userId)
    {
        return await _context.Crates
            .Where(c => c.OwnerId == userId)
            .Select(c => new CrateDto
            {
                Id = c.Id,
                Name = c.Name,
            }).ToListAsync();
    }

    public async Task<CrateDto> RenameCrateAsync(Guid crateId, string userId, string newName)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            throw new Exception("Crate not found");

        crate.Rename(newName);

        await _context.SaveChangesAsync();

        return new CrateDto
        {
            Id = crate.Id,
            Name = crate.Name,
        };
    }

    public async Task AddFileToCrateAsync(Guid crateId, string userId, FileObject file)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            throw new Exception("Crate not found");

        crate.AddFile(file);

        await _context.SaveChangesAsync();
    }

    public async Task UploadFileAsync(Guid crateId, string userId, UploadFileDto file)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            throw new Exception("Crate not found");

        var storedName = $"{Guid.NewGuid()}_{file.FileName}";
        await _fileStorageService.UploadAsync(file.FileStream, storedName);

        var fileObj = new FileObject
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            StoredName = storedName,
            ContentType = file.ContentType,
            Size = file.Size,
            CrateId = crateId
        };

        crate.AddFile(fileObj);
        await _context.SaveChangesAsync();
    }

    public async Task<(Stream FileStream, string FIleName)> DownloadFileAsync(Guid crateId, string userId, Guid fileId)
    {
        var crate = await _context.Crates
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null) throw new Exception("Crate not found");

        var file = crate.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) throw new Exception("File not found");

        var stream = await _fileStorageService.DownloadAsync(file.StoredName);
        return (stream, file.FileName);
    }
}