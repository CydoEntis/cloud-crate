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
        var crate = new Crate
        {
            Id = Guid.NewGuid(),
            Name = crateName,
            OwnerId = userId
        };

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
}