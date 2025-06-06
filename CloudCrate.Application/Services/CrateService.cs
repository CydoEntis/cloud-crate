using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Application.Services;

public class CrateService : ICrateService
{
    private readonly IAppDbContext _context;

    public CrateService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CrateDto>> CreateCrateAsync(string userId, string crateName)
    {
        var crate = Crate.Create(crateName, userId);
        _context.Crates.Add(crate);
        await _context.SaveChangesAsync();

        return Result<CrateDto>.Success(new CrateDto
        {
            Id = crate.Id,
            Name = crate.Name
        });
    }

    public async Task<Result<IEnumerable<CrateDto>>> GetAllCratesAsync(string userId)
    {
        var crates = await _context.Crates
            .Where(c => c.OwnerId == userId)
            .Select(c => new CrateDto
            {
                Id = c.Id,
                Name = c.Name
            }).ToListAsync();

        return Result<IEnumerable<CrateDto>>.Success(crates);
    }

    public async Task<Result<CrateDto>> RenameCrateAsync(string userId, Guid crateId, string crateName)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);

        if (crate == null)
            return Result<CrateDto>.Failure("Crate not found");

        crate.Rename(crateName);
        await _context.SaveChangesAsync();

        return Result<CrateDto>.Success(new CrateDto
        {
            Id = crate.Id,
            Name = crate.Name
        });
    }
}