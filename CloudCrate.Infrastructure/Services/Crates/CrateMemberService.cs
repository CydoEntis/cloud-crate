using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateMemberService : ICrateMemberService
{
    private readonly IAppDbContext _context;

    public CrateMemberService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CrateMember?>> GetUserRoleAsync(Guid crateId, string userId)
    {
        try
        {
            var role = await _context.CrateMembers
                .FirstOrDefaultAsync(p => p.CrateId == crateId && p.UserId == userId);

            return Result<CrateMember?>.Success(role);
        }
        catch (Exception ex)
        {
            return Result<CrateMember?>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<bool>> IsOwnerAsync(Guid crateId, string userId)
    {
        try
        {
            var roleResult = await GetUserRoleAsync(crateId, userId);
            if (!roleResult.Succeeded)
                return Result<bool>.Failure(roleResult.Errors);

            return Result<bool>.Success(roleResult.Value?.Role == CrateRole.Owner);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<bool>> CanUserUploadAsync(Guid crateId, string userId)
    {
        try
        {
            var roleResult = await GetUserRoleAsync(crateId, userId);
            if (!roleResult.Succeeded)
                return Result<bool>.Failure(roleResult.Errors);

            var canUpload = roleResult.Value is not null && CrateRolePermissions.CanUpload(roleResult.Value.Role);
            return Result<bool>.Success(canUpload);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<bool>> CanUserDownloadAsync(Guid crateId, string userId)
    {
        try
        {
            var roleResult = await GetUserRoleAsync(crateId, userId);
            if (!roleResult.Succeeded)
                return Result<bool>.Failure(roleResult.Errors);

            var canDownload = roleResult.Value is not null && CrateRolePermissions.CanDownload(roleResult.Value.Role);
            return Result<bool>.Success(canDownload);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<bool>> CanUserDeleteFileAsync(Guid crateId, string userId)
    {
        try
        {
            var roleResult = await GetUserRoleAsync(crateId, userId);
            if (!roleResult.Succeeded)
                return Result<bool>.Failure(roleResult.Errors);

            var canDelete = roleResult.Value is not null && CrateRolePermissions.CanDeleteFiles(roleResult.Value.Role);
            return Result<bool>.Success(canDelete);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<bool>> CanUserManagePermissionsAsync(Guid crateId, string userId)
    {
        try
        {
            var roleResult = await GetUserRoleAsync(crateId, userId);
            if (!roleResult.Succeeded)
                return Result<bool>.Failure(roleResult.Errors);

            var canManage = roleResult.Value is not null &&
                            CrateRolePermissions.CanManagePermissions(roleResult.Value.Role);
            return Result<bool>.Success(canManage);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role)
    {
        try
        {
            var permission = await _context.CrateMembers
                .FirstOrDefaultAsync(p => p.CrateId == crateId && p.UserId == userId);

            if (permission == null)
            {
                permission = CrateMember.Create(crateId, userId, role);
                _context.CrateMembers.Add(permission);
            }
            else
            {
                permission.Role = role;
                _context.CrateMembers.Update(permission);
            }

            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Errors.Common.InternalServerError with
            {
                Message = $"{Errors.Common.InternalServerError.Message} ({ex.Message})"
            });
        }
    }

    public async Task RemoveAllMembersFromCrateAsync(Guid crateId)
    {
        var members = await _context.CrateMembers
            .Where(m => m.CrateId == crateId)
            .ToListAsync();

        _context.CrateMembers.RemoveRange(members);
    }
}