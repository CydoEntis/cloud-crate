using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services;

public class CratePermissionsService : ICratePermissionService
{
    private readonly IAppDbContext _context;

    public CratePermissionsService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<CratePermission?> GetUserPermissionsAsync(Guid crateId, string userId)
    {
        return await _context.CratePermissions
            .FirstOrDefaultAsync(p => p.CrateId == crateId && p.UserId == userId);
    }

    public async Task<bool> IsOwnerAsync(Guid crateId, string userId)
    {
        return await _context.Crates
            .AnyAsync(c => c.Id == crateId && c.UserId == userId);
    }

    public async Task<bool> CanUserUploadAsync(Guid crateId, string userId)
    {
        if (await IsOwnerAsync(crateId, userId))
            return true;

        var permission = await GetUserPermissionsAsync(crateId, userId);
        return permission?.CanUpload ?? false;
    }

    public async Task<bool> CanUserDownloadAsync(Guid crateId, string userId)
    {
        if (await IsOwnerAsync(crateId, userId))
            return true;

        var permission = await GetUserPermissionsAsync(crateId, userId);
        return permission?.CanDownload ?? false;
    }

    public async Task<bool> CanUserDeleteFileAsync(Guid crateId, string userId)
    {
        if (await IsOwnerAsync(crateId, userId))
            return true;

        var permission = await GetUserPermissionsAsync(crateId, userId);
        return permission?.CanDelete ?? false;
    }
}