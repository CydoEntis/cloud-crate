using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICratePermissionService
{
    Task<CratePermission> GetUserPermissionsAsync(Guid crateId, string userId);
    Task<bool> CanUserUploadAsync(Guid crateId, string userId);
    Task<bool> CanUserDownloadAsync(Guid crateId, string userId);
    Task<bool> CanUserDeleteFileAsync(Guid crateId, string userId);
    Task<bool> IsOwnerAsync(Guid crateId, string userId);
}
