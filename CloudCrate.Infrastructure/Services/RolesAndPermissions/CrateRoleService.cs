using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;
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

    private async Task<Result<bool>> HasRoleAsync(
        Guid crateId,
        string userId,
        CrateRole[] allowedRoles,
        Error? error = null)
    {
        var member = await _context.CrateMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (member == null || !allowedRoles.Contains(member.Role))
            return Result<bool>.Failure(error ?? new CrateUnauthorizedError());

        return Result<bool>.Success(true);
    }

    public Task<Result<bool>> IsOwner(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, new[] { CrateRole.Owner }, new CrateUnauthorizedError("User is not the owner"));

    public Task<Result<bool>> CanManageCrate(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, new[] { CrateRole.Owner },
            new CrateUnauthorizedError("Cannot manage this crate"));

    public Task<Result<bool>> CanContribute(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId,
            new[] { CrateRole.Owner, CrateRole.Contributor },
            new CrateUnauthorizedError("Cannot contribute to this crate"));

    public Task<Result<bool>> CanUpload(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId, new[] { CrateRole.Owner, CrateRole.Contributor, CrateRole.Uploader },
            new CrateUnauthorizedError("Cannot upload to this crate"));

    public Task<Result<bool>> CanView(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId,
            new[] { CrateRole.Owner, CrateRole.Contributor, CrateRole.Uploader, CrateRole.Viewer },
            new CrateUnauthorizedError("Cannot view this crate"));

    public Task<Result<bool>> CanDownload(Guid crateId, string userId) =>
        HasRoleAsync(crateId, userId,
            new[]
            {
                CrateRole.Owner, CrateRole.Contributor, CrateRole.Uploader, CrateRole.Viewer, CrateRole.Downloader
            },
            new CrateUnauthorizedError("Cannot download this crate"));
}