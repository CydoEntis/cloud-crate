using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public class CrateMemberService : ICrateMemberService
{
    private readonly IAppDbContext _context;

    public CrateMemberService(IAppDbContext context) => _context = context;

    public async Task<CrateMember?> GetCrateMemberAsync(Guid crateId, string userId)
    {
        return await _context.CrateMembers
            .AsNoTracking()
            .Include(m => m.Crate)
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);
    }

    public async Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role)
    {
        var member = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (member == null)
        {
            member = CrateMember.Create(crateId, userId, role);
            _context.CrateMembers.Add(member);
        }
        else
        {
            member.Role = role;
            _context.CrateMembers.Update(member);
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task RemoveAllMembersFromCrateAsync(Guid crateId)
    {
        var members = await _context.CrateMembers
            .Where(m => m.CrateId == crateId)
            .ToListAsync();

        _context.CrateMembers.RemoveRange(members);
        await _context.SaveChangesAsync();
    }
}