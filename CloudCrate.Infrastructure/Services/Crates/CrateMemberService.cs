using CloudCrate.Application.DTOs;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.CrateMember.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class CrateMemberService : ICrateMemberService
{
    private readonly AppDbContext _context;
    private readonly ICrateRoleService _roleService;
    private readonly IBatchMembershipService _batchMembershipService;
    private readonly ILogger<CrateMemberService> _logger;

    public CrateMemberService(
        AppDbContext context,
        ICrateRoleService roleService,
        IBatchMembershipService batchMembershipService,
        ILogger<CrateMemberService> logger)
    {
        _context = context;
        _roleService = roleService;
        _batchMembershipService = batchMembershipService;
        _logger = logger;
    }

    public async Task<Result<PaginatedResult<CrateMemberResponse>>> GetCrateMembersAsync(
        Guid crateId,
        string requestingUserId,
        CrateMemberQueryParameters parameters)
    {
        var canViewResult = await _roleService.CanView(crateId, requestingUserId);
        if (!canViewResult.IsSuccess)
            return Result<PaginatedResult<CrateMemberResponse>>.Failure(canViewResult.GetError());

        var query = _context.CrateMembers
            .Include(m => m.User)
            .Where(m => m.CrateId == crateId);

        query = query.ApplyMemberSearch(parameters.SearchTerm)
            .ApplyMemberOrdering(parameters);

        var pagedEntities = await query.PaginateAsync(parameters.Page, parameters.PageSize);

        var responses = pagedEntities.Items.Select(entity => new CrateMemberResponse
        {
            UserId = entity.UserId,
            DisplayName = entity.User?.DisplayName ?? "Unknown",
            Email = entity.User?.Email ?? string.Empty,
            Role = entity.Role,
            ProfilePicture = entity.User?.ProfilePictureUrl ?? string.Empty,
            JoinedAt = entity.JoinedAt
        }).ToList();

        return Result<PaginatedResult<CrateMemberResponse>>.Success(
            PaginatedResult<CrateMemberResponse>.Create(responses, pagedEntities.TotalCount, parameters.Page,
                parameters.PageSize));
    }

    public async Task<Result<CrateMemberAvatarResponse>> GetMemberAvatarsAsync(Guid crateId, string requestingUserId)
    {
        var canViewResult = await _roleService.CanView(crateId, requestingUserId);
        if (!canViewResult.IsSuccess)
            return Result<CrateMemberAvatarResponse>.Failure(canViewResult.GetError());

        var ownerEntity = await _context.CrateMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.Role == CrateRole.Owner);

        if (ownerEntity == null)
            return Result<CrateMemberAvatarResponse>.Failure(new ValidationError("Crate has no owner"));

        var recentMemberEntities = await _context.CrateMembers
            .Include(m => m.User)
            .Where(m => m.CrateId == crateId && m.Role != CrateRole.Owner)
            .OrderByDescending(m => m.JoinedAt)
            .Take(4)
            .ToListAsync();

        var totalMemberCount = await _context.CrateMembers.CountAsync(m => m.CrateId == crateId);
        var remainingCount = Math.Max(0, totalMemberCount - 1 - recentMemberEntities.Count);

        var response = new CrateMemberAvatarResponse()
        {
            Owner = new CrateMemberResponse
            {
                UserId = ownerEntity.UserId,
                DisplayName = ownerEntity.User?.DisplayName ?? "Unknown",
                Email = ownerEntity.User?.Email ?? string.Empty,
                Role = ownerEntity.Role,
                ProfilePicture = ownerEntity.User?.ProfilePictureUrl ?? string.Empty,
                JoinedAt = ownerEntity.JoinedAt
            },
            RecentMembers = recentMemberEntities.Select(entity => new CrateMemberResponse
            {
                UserId = entity.UserId,
                DisplayName = entity.User?.DisplayName ?? "Unknown",
                Email = entity.User?.Email ?? string.Empty,
                Role = entity.Role,
                ProfilePicture = entity.User?.ProfilePictureUrl ?? string.Empty,
                JoinedAt = entity.JoinedAt
            }).ToList(),
            RemainingCount = remainingCount
        };

        return Result<CrateMemberAvatarResponse>.Success(response);
    }

    public async Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role, string requestingUserId)
    {
        var canManageResult = await _roleService.CanManageCrate(crateId, requestingUserId);
        if (!canManageResult.IsSuccess || !canManageResult.GetValue())
            return Result.Failure(new ForbiddenError("Only crate owners can assign roles"));

        var memberEntity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        var requestingMemberEntity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == requestingUserId);

        if (requestingMemberEntity == null)
            return Result.Failure(new ForbiddenError("Requesting user is not a member of this crate"));

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (memberEntity == null)
            {
                var newMember = CrateMember.Create(crateId, userId, role);
                _context.CrateMembers.Add(newMember.ToEntity(crateId));
            }
            else
            {
                var memberDomain = memberEntity.ToDomain();
                var requestingMemberDomain = requestingMemberEntity.ToDomain();

                memberDomain.UpdateRole(role, requestingMemberDomain);
                _context.CrateMembers.Update(memberDomain.ToEntity(crateId));
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to assign role for user {UserId} in crate {CrateId}", userId, crateId);
            return Result.Failure(new InternalError($"Failed to assign role: {ex.Message}"));
        }
    }

    public async Task<Result> RemoveMemberAsync(Guid crateId, string userId, string requestingUserId)
    {
        var memberEntity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (memberEntity == null)
            return Result.Failure(new NotFoundError("Membership not found"));

        var requestingMemberEntity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == requestingUserId);

        if (requestingMemberEntity == null)
            return Result.Failure(new ForbiddenError("Requesting user is not a member of this crate"));

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var memberDomain = memberEntity.ToDomain();
            var requestingMemberDomain = requestingMemberEntity.ToDomain();

            memberDomain.ValidateRemoval(requestingMemberDomain);

            _context.CrateMembers.Remove(memberEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to remove member {UserId} from crate {CrateId}", userId, crateId);
            return Result.Failure(new InternalError($"Failed to remove member: {ex.Message}"));
        }
    }

    public async Task<Result> LeaveCrateAsync(Guid crateId, string userId)
    {
        var memberEntity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (memberEntity == null)
            return Result.Failure(new NotFoundError("Membership not found"));

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var memberDomain = memberEntity.ToDomain();
            memberDomain.ValidateLeaving();

            _context.CrateMembers.Remove(memberEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("User {UserId} left crate {CrateId}", userId, crateId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to leave crate {CrateId}", crateId);
            return Result.Failure(new InternalError($"Failed to leave crate: {ex.Message}"));
        }
    }

    public async Task<Result<int>> LeaveCratesAsync(IEnumerable<Guid> crateIds, string userId)
    {
        if (!crateIds.Any())
            return Result<int>.Failure(new ValidationError("No crates provided"));

        return await _batchMembershipService.LeaveCratesAsync(userId, crateIds);
    }

    public async Task RemoveAllMembersFromCrateAsync(Guid crateId)
    {
        await _context.CrateMembers
            .Where(m => m.CrateId == crateId)
            .ExecuteDeleteAsync();
    }

    public async Task<CrateMember?> GetCrateMemberAsync(Guid crateId, string userId)
    {
        var entity = await _context.CrateMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        return entity?.ToDomain();
    }
}