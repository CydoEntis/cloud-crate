using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.CrateMember.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Queries;
using CloudCrate.Infrastructure.Services.RolesAndPermissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class CrateMemberService : ICrateMemberService
{
    private readonly AppDbContext _context;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IBatchMembershipService _batchMembershipService;
    private readonly ILogger<CrateMemberService> _logger;

    public CrateMemberService(
        AppDbContext context,
        ICrateRoleService crateRoleService,
        IBatchMembershipService batchMembershipService,
        ILogger<CrateMemberService> logger)
    {
        _context = context;
        _crateRoleService = crateRoleService;
        _batchMembershipService = batchMembershipService;
        _logger = logger;
    }

    public async Task<Result<PaginatedResult<CrateMemberResponse>>> GetCrateMembersAsync(
        Guid crateId,
        string requestingUserId,
        CrateMemberQueryParameters parameters)
    {
        var role = await _crateRoleService.GetUserRole(crateId, requestingUserId);
        if (role == null)
            return Result<PaginatedResult<CrateMemberResponse>>.Failure(
                new CrateUnauthorizedError("Not a member of this crate"));

        var query = _context.CrateMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.CrateId == crateId);

        query = query
            .ApplyMemberSearch(parameters.SearchTerm)
            .ApplyMemberFiltering(parameters)
            .ApplyMemberOrdering(parameters);

        var totalCount = await query.CountAsync();

        List<CrateMemberEntity> entities;
        int page, pageSize;

        if (parameters.Limit.HasValue)
        {
            entities = await query.Take(parameters.Limit.Value).ToListAsync();
            page = 1;
            pageSize = parameters.Limit.Value;
        }
        else
        {
            entities = await query
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync();
            page = parameters.Page;
            pageSize = parameters.PageSize;
        }

        var responses = entities.Select(entity => new CrateMemberResponse
        {
            UserId = entity.UserId,
            DisplayName = entity.User?.DisplayName ?? "Unknown",
            Email = entity.User?.Email ?? string.Empty,
            Role = entity.Role,
            ProfilePicture = entity.User?.ProfilePictureUrl ?? string.Empty,
            JoinedAt = entity.JoinedAt
        }).ToList();

        var result = PaginatedResult<CrateMemberResponse>.Create(responses, totalCount, page, pageSize);

        return Result<PaginatedResult<CrateMemberResponse>>.Success(result);
    }

    public async Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role, string requestingUserId)
    {
        var requestingRole = await _crateRoleService.GetUserRole(crateId, requestingUserId);
        if (requestingRole == null)
            return Result.Failure(new CrateUnauthorizedError("Not a member of this crate"));

        var canAssignRoles = requestingRole switch
        {
            CrateRole.Owner => true,
            CrateRole.Manager => true,
            CrateRole.Member => false,
            _ => false
        };

        if (!canAssignRoles)
            return Result.Failure(new ForbiddenError("Only owners and managers can assign roles"));

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

    public async Task<Result> AcceptInviteRoleAsync(Guid crateId, string userId, CrateRole role, string invitingUserId)
    {
        var invitingRole = await _crateRoleService.GetUserRole(crateId, invitingUserId);
        if (invitingRole == null)
            return Result.Failure(new CrateUnauthorizedError("Inviting user is not a member of this crate"));

        var canInvite = invitingRole switch
        {
            CrateRole.Owner => true,
            CrateRole.Manager => true,
            CrateRole.Member => false,
            _ => false
        };

        if (!canInvite)
            return Result.Failure(new ForbiddenError("Invite creator no longer has permission to add members"));

        var memberEntity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (memberEntity != null)
            return Result.Failure(new BusinessRuleError("User is already a member of this crate"));

        try
        {
            var newMember = CrateMember.Create(crateId, userId, role);
            _context.CrateMembers.Add(newMember.ToEntity(crateId));

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept invite for user {UserId} in crate {CrateId}", userId, crateId);
            return Result.Failure(new InternalError($"Failed to accept invite: {ex.Message}"));
        }
    }
}