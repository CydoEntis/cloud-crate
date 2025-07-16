using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services;

public class CrateUserRolesService : ICrateUserRoleService
{
    private readonly IAppDbContext _context;

    public CrateUserRolesService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<CrateUserRole?> GetUserRoleAsync(Guid crateId, string userId)
    {
        return await _context.CrateUserRoles
            .FirstOrDefaultAsync(p => p.CrateId == crateId && p.UserId == userId);
    }

    public async Task<bool> IsOwnerAsync(Guid crateId, string userId)
    {
        var permission = await GetUserRoleAsync(crateId, userId);
        return permission?.Role == CrateRole.Owner;
    }

    public async Task<bool> CanUserUploadAsync(Guid crateId, string userId)
    {
        var permission = await GetUserRoleAsync(crateId, userId);
        return permission is not null && CrateRolePermissions.CanUpload(permission.Role);
    }

    public async Task<bool> CanUserDownloadAsync(Guid crateId, string userId)
    {
        var permission = await GetUserRoleAsync(crateId, userId);
        return permission is not null && CrateRolePermissions.CanDownload(permission.Role);
    }

    public async Task<bool> CanUserDeleteFileAsync(Guid crateId, string userId)
    {
        var permission = await GetUserRoleAsync(crateId, userId);
        return permission is not null && CrateRolePermissions.CanDeleteFiles(permission.Role);
    }

    public async Task<bool> CanUserManagePermissionsAsync(Guid crateId, string userId)
    {
        var permission = await GetUserRoleAsync(crateId, userId);
        return permission is not null && CrateRolePermissions.CanManagePermissions(permission.Role);
    }
    
    public async Task AssignRoleAsync(Guid crateId, string userId, CrateRole role)
    {
        var permission = await _context.CrateUserRoles
            .FirstOrDefaultAsync(p => p.CrateId == crateId && p.UserId == userId);

        if (permission == null)
        {
            permission = new CrateUserRole
            {
                CrateId = crateId,
                UserId = userId,
                Role = role
            };
            _context.CrateUserRoles.Add(permission);
        }
        else
        {
            permission.Role = role;
            _context.CrateUserRoles.Update(permission);
        }

        await _context.SaveChangesAsync();
    }

}