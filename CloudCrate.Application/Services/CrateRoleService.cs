using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Domain.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CloudCrate.Application.Services;

public sealed record CrateUnauthorizedError(string Message = "User is not authorized for this crate") : Error(Message);

public class CrateRoleService : ICrateRoleService
{
    private readonly ICrateMemberService _crateMemberService;

    public CrateRoleService(ICrateMemberService crateMemberService)
    {
        _crateMemberService = crateMemberService;
    }

    private async Task<Result<bool>> HasRoleAsync(Guid crateId, string userId, params CrateRole[] allowedRoles)
    {
        var roleResult = await _crateMemberService.GetUserRoleAsync(crateId, userId);
        if (roleResult.IsFailure)
            return Result<bool>.Failure(roleResult.Error!);

        return allowedRoles.Contains(roleResult.Value!)
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(new CrateUnauthorizedError());
    }

    public Task<Result<bool>> CanManageCrate(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, CrateRole.Owner);

    public Task<Result<bool>> CanContribute(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, CrateRole.Owner, CrateRole.Contributor);

    public Task<Result<bool>> CanUpload(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, CrateRole.Owner, CrateRole.Contributor, CrateRole.Uploader);

    public Task<Result<bool>> CanView(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, CrateRole.Owner, CrateRole.Contributor, CrateRole.Uploader, CrateRole.Viewer);

    public Task<Result<bool>> CanDownload(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, CrateRole.Owner, CrateRole.Contributor, CrateRole.Uploader, CrateRole.Viewer,
            CrateRole.Downloader);
}
