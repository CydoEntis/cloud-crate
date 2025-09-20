using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.RolesAndPermissions;

public sealed record CrateUnauthorizedError(string Message = "User is not authorized for this crate") : Error(Message);

public class CrateRoleService : ICrateRoleService
{
    private readonly AppDbContext _context;

    public CrateRoleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CrateRole?> GetUserRole(Guid crateId, string userId)
    {
        var member = await _context.CrateMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);
            
        return member?.Role;
    }

    
}