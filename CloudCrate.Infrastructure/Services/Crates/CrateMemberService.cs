using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateMemberService : ICrateMemberService
{
    private readonly IAppDbContext _context;

    public CrateMemberService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Crate>> GetCratesForUserAsync(string userId)
    {
        return await _context.CrateMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.Crate)
            .Include(c => c.Members)
            .Include(c => c.Files)
            .ToListAsync();
    }

    public async Task<DateTime?> GetUsersJoinDateAsync(Guid crateId, string userId)
    {
        var member = await _context.CrateMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        return member?.JoinedDate;
    }

    public async Task<CrateMember?> GetCrateOwnerAsync(Guid crateId)
    {
        return await _context.CrateMembers
            .Where(m => m.CrateId == crateId && m.Role == CrateRole.Owner)
            .SingleOrDefaultAsync();
    }

    public async Task<Result<CrateRole>> GetUserRoleAsync(Guid crateId, string userId)
    {
        var member = await _context.CrateMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (member is null)
            return Result<CrateRole>.Failure(new UnauthorizedError("User is not a member of this crate"));

        return Result<CrateRole>.Success(member.Role);
    }

    public async Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role)
    {
        try
        {
            var member = await _context.CrateMembers
                .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

            if (member is null)
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
        catch (Exception ex)
        {
            return Result.Failure(new InternalError($"Failed to assign role: {ex.Message}"));
        }
    }

    public async Task RemoveAllMembersFromCrateAsync(Guid crateId)
    {
        var members = await _context.CrateMembers
            .Where(m => m.CrateId == crateId)
            .ToListAsync();

        _context.CrateMembers.RemoveRange(members);
    }
}