using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services;

public class CrateService : ICrateService
{
    private readonly AppDbContext _context;

    public CrateService(AppDbContext context)
    {
        _context = context;
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
}