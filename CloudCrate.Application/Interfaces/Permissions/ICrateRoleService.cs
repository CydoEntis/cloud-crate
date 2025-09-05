using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Permissions;

public interface ICrateRoleService
{
    Task<Result<bool>> CanManageCrate(Guid crateId, string userId);
    Task<Result<bool>> CanContribute(Guid crateId, string userId);
    Task<Result<bool>> CanUpload(Guid crateId, string userId);
    Task<Result<bool>> CanView(Guid crateId, string userId);
    Task<Result<bool>> CanDownload(Guid crateId, string userId);
}