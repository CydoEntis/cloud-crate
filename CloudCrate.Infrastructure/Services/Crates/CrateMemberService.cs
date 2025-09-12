using CloudCrate.Application.DTOs;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Transactions;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class CrateMemberService : ICrateMemberService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly ICrateRoleService _roleService;
    private readonly IBatchMembershipService _batchMembershipService;
    private readonly ITransactionService _transactionService;
    private readonly ILogger<CrateMemberService> _logger;

    public CrateMemberService(
        AppDbContext context,
        IUserService userService,
        ICrateRoleService roleService,
        IBatchMembershipService batchMembershipService,
        ITransactionService transactionService,
        ILogger<CrateMemberService> logger)
    {
        _context = context;
        _userService = userService;
        _roleService = roleService;
        _batchMembershipService = batchMembershipService;
        _transactionService = transactionService;
        _logger = logger;
    }

    public async Task<CrateMember?> GetCrateMemberAsync(Guid crateId, string userId)
    {
        var entity = await _context.CrateMembers
            .AsNoTracking()
            .Include(m => m.Crate)
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        return entity?.ToDomain();
    }

    private async Task<List<CrateMember>> GetMostRecentMembersAsync(Guid crateId, int count = 4)
    {
        var entities = await _context.CrateMembers
            .Where(m => m.CrateId == crateId && m.Role != CrateRole.Owner)
            .OrderByDescending(m => m.JoinedAt)
            .Take(count)
            .ToListAsync();

        return entities.Select(CrateMemberMapper.ToDomain).ToList();
    }

    private async Task<CrateMember?> GetCrateOwnerAsync(Guid crateId)
    {
        var entity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.Role == CrateRole.Owner);

        return entity?.ToDomain();
    }

    public async Task<Result<CrateMemberPreviewResponse>> GetCrateMemberPreviewAsync(Guid crateId, string requestingUserId)
    {
        var canViewResult = await _roleService.CanView(crateId, requestingUserId);
        if (!canViewResult.IsSuccess)
            return Result<CrateMemberPreviewResponse>.Failure(canViewResult.Error!);

        var recentMembers = await GetMostRecentMembersAsync(crateId, 4);
        if (!recentMembers.Any())
            return Result<CrateMemberPreviewResponse>.Failure(new NotFoundError("No members found"));

        var owner = await GetCrateOwnerAsync(crateId);
        if (owner == null)
            return Result<CrateMemberPreviewResponse>.Failure(new ValidationError("Crate has no owner"));

        // Build user lookup
        var userIds = recentMembers.Select(m => m.UserId).Append(owner.UserId).Distinct().ToList();
        var users = await _userService.GetUsersByIdsAsync(userIds);
        var userLookup = users.ToDictionary(u => u.Id, u => u);

        // Map owner and recent members
        var ownerResponse = owner.ToCrateMemberResponse(userLookup);
        var recentMembersResponse = recentMembers.Select(m => m.ToCrateMemberResponse(userLookup)).ToList();

        var remainingCount = Math.Max(0,
            (await _context.CrateMembers.CountAsync(m => m.CrateId == crateId)) - 1 - recentMembersResponse.Count);

        return Result<CrateMemberPreviewResponse>.Success(new CrateMemberPreviewResponse
        {
            Owner = ownerResponse,
            RecentMembers = recentMembersResponse,
            RemainingCount = remainingCount
        });
    }


    public async Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role, string requestingUserId)
    {
        var crateExists = await _context.Crates.AnyAsync(c => c.Id == crateId);
        if (!crateExists) return Result.Failure(new NotFoundError("Crate not found"));

        var isRequesterOwner = await _context.CrateMembers
            .AnyAsync(m => m.CrateId == crateId && m.UserId == requestingUserId && m.Role == CrateRole.Owner);
        if (!isRequesterOwner) return Result.Failure(new ForbiddenError("Only crate owners can assign roles"));

        var entity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

        if (entity == null)
        {
            var member = CrateMember.Create(crateId, userId, role);
            _context.CrateMembers.Add(member.ToEntity(crateId));
        }
        else
        {
            var memberDomain = entity.ToDomain();
            memberDomain.UpdateRole(role); 
            _context.CrateMembers.Update(memberDomain.ToEntity(crateId));
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid crateId, string userId, string requestingUserId)
    {
        var entity = await _context.CrateMembers
            .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);
        if (entity == null) return Result.Failure(new NotFoundError("Membership not found"));

        var memberDomain = entity.ToDomain();
        if (memberDomain.Role == CrateRole.Owner)
            return Result.Failure(new ValidationError("Cannot remove the owner of the crate"));

        var isRequesterOwner = await _context.CrateMembers
            .AnyAsync(m => m.CrateId == crateId && m.UserId == requestingUserId && m.Role == CrateRole.Owner);
        if (!isRequesterOwner) return Result.Failure(new ForbiddenError("Only crate owners can remove members"));

        _context.CrateMembers.Remove(entity);
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task RemoveAllMembersFromCrateAsync(Guid crateId)
    {
        var entities = await _context.CrateMembers
            .Where(m => m.CrateId == crateId)
            .ToListAsync();

        _context.CrateMembers.RemoveRange(entities);
        await _context.SaveChangesAsync();
    }

    public async Task<Result<List<CrateMemberResponse>>> GetMembersForCrateAsync(Guid crateId, string requestingUserId)
    {
        var isMember = await _context.CrateMembers
            .AnyAsync(m => m.CrateId == crateId && m.UserId == requestingUserId);
        if (!isMember)
            return Result<List<CrateMemberResponse>>.Failure(new ForbiddenError("You are not a member of this crate"));

        var entities = await _context.CrateMembers
            .Where(m => m.CrateId == crateId)
            .ToListAsync();

        if (!entities.Any()) return Result<List<CrateMemberResponse>>.Success(new List<CrateMemberResponse>());

        var members = entities.Select(CrateMemberMapper.ToDomain).ToList();
        var userIds = members.Select(m => m.UserId).ToList();
        var users = await _userService.GetUsersByIdsAsync(userIds);
        var userMap = users.ToDictionary(u => u.Id, u => u);

        var responses = members.Select(m =>
        {
            userMap.TryGetValue(m.UserId, out var user);
            return new CrateMemberResponse
            {
                UserId = m.UserId,
                DisplayName = user?.DisplayName ?? "Unknown",
                Email = user?.Email ?? string.Empty,
                Role = m.Role,
                ProfilePicture = user?.ProfilePictureUrl ?? string.Empty
            };
        }).ToList();

        return Result<List<CrateMemberResponse>>.Success(responses);
    }

    public async Task<Result> LeaveCrateAsync(Guid crateId, string userId)
    {
        var crateEntity = await _context.Crates.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crateEntity == null) return Result.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();
        var member = crateDomain.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null) return Result.Failure(new NotFoundError("Membership not found"));

        if (member.Role == CrateRole.Owner)
            return Result.Failure(new ValidationError("Owner cannot leave the crate"));

        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            var leaveResult = await _batchMembershipService.LeaveCratesAsync(userId, new[] { crateId });
            if (!leaveResult.IsSuccess)
                throw new Exception(leaveResult.Error?.Message ?? "Failed to leave crate");
        });

        if (!transactionResult.IsSuccess) return Result.Failure(transactionResult.Error);

        _logger.LogInformation("User {UserId} left crate {CrateId}", userId, crateId);
        return Result.Success();
    }

    public async Task<Result<int>> LeaveCratesAsync(IEnumerable<Guid> crateIds, string userId)
    {
        if (!crateIds.Any()) return Result<int>.Failure(new ValidationError("No crates provided"));

        int leftCount = 0;

        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            var leaveResult = await _batchMembershipService.LeaveCratesAsync(userId, crateIds);
            if (!leaveResult.IsSuccess)
                throw new Exception(leaveResult.Error?.Message ?? "Failed to leave crates");

            leftCount = leaveResult.Value;
        });

        if (!transactionResult.IsSuccess) return Result<int>.Failure(transactionResult.Error);

        _logger.LogInformation("User {UserId} bulk-left {Count} crates", userId, leftCount);
        return Result<int>.Success(leftCount);
    }
}
