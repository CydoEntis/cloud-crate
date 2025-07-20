using CloudCrate.Application.Common.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateUserRoleService
{
    Task<Result<CrateUserRole?>> GetUserRoleAsync(Guid crateId, string userId);
    Task<Result<bool>> CanUserUploadAsync(Guid crateId, string userId);
    Task<Result<bool>> CanUserDownloadAsync(Guid crateId, string userId);
    Task<Result<bool>> CanUserDeleteFileAsync(Guid crateId, string userId);
    Task<Result<bool>> CanUserManagePermissionsAsync(Guid crateId, string userId);
    Task<Result<bool>> IsOwnerAsync(Guid crateId, string userId);

    Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role);
}