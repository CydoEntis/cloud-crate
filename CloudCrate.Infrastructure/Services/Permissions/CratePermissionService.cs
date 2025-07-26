using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;

namespace CloudCrate.Infrastructure.Services.Permissions;

public class CratePermissionService : ICratePermissionService
{
    private readonly ICrateMemberService _crateMemberService;

    public CratePermissionService(ICrateMemberService crateMemberService)
    {
        _crateMemberService = crateMemberService;
    }

    public async Task<Result<bool>> CheckViewPermissionAsync(Guid crateId, string userId)
    {
        var result = await _crateMemberService.CanUserDownloadAsync(crateId, userId);
        if (!result.Succeeded)
            return Result<bool>.Failure(result.Errors);
        if (!result.Value)
            return Result<bool>.Failure(Errors.User.Unauthorized);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> CheckUploadPermissionAsync(Guid crateId, string userId)
    {
        var result = await _crateMemberService.CanUserUploadAsync(crateId, userId);
        if (!result.Succeeded)
            return Result<bool>.Failure(result.Errors);
        if (!result.Value)
            return Result<bool>.Failure(Errors.User.Unauthorized);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> CheckDeletePermissionAsync(Guid crateId, string userId)
    {
        var result = await _crateMemberService.CanUserDeleteFileAsync(crateId, userId);
        if (!result.Succeeded)
            return Result<bool>.Failure(result.Errors);
        if (!result.Value)
            return Result<bool>.Failure(Errors.User.Unauthorized);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> CheckOwnerPermissionAsync(Guid crateId, string userId)
    {
        var isOwnerResult = await _crateMemberService.IsOwnerAsync(crateId, userId);
        if (!isOwnerResult.Succeeded)
            return Result<bool>.Failure(isOwnerResult.Errors);
        if (!isOwnerResult.Value)
            return Result<bool>.Failure(Errors.User.Unauthorized);
        return Result<bool>.Success(true);
    }
}