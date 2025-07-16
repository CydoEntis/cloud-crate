using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateUserRoleService
{
    Task<CrateUserRole?> GetUserRoleAsync(Guid crateId, string userId);
    Task<bool> CanUserUploadAsync(Guid crateId, string userId);
    Task<bool> CanUserDownloadAsync(Guid crateId, string userId);
    Task<bool> CanUserDeleteFileAsync(Guid crateId, string userId);
    Task<bool> CanUserManagePermissionsAsync(Guid crateId, string userId);
    Task<bool> IsOwnerAsync(Guid crateId, string userId);

    Task AssignRoleAsync(Guid crateId, string userId, CrateRole role);
}