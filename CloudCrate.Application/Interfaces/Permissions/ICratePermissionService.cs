using CloudCrate.Application.Common.Models;

namespace CloudCrate.Application.Interfaces.Permissions;

public interface ICratePermissionService
{
    Task<Result<bool>> CheckViewPermissionAsync(Guid crateId, string userId);
    Task<Result<bool>> CheckUploadPermissionAsync(Guid crateId, string userId);
    Task<Result<bool>> CheckDeletePermissionAsync(Guid crateId, string userId);
    Task<Result<bool>> CheckOwnerPermissionAsync(Guid crateId, string userId);
}