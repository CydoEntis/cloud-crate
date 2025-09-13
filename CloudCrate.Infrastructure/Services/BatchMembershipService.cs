using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services;

public class BatchMembershipService : IBatchMembershipService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BatchMembershipService> _logger;
    private const int BatchSize = 500;

    public BatchMembershipService(
        AppDbContext context,
        ILogger<BatchMembershipService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<int>> LeaveCratesAsync(string userId, IEnumerable<Guid> crateIds)
    {
        var crateIdList = crateIds.ToList();
        var totalLeftCount = 0;

        while (crateIdList.Any())
        {
            var batch = crateIdList.Take(BatchSize).ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var memberships = await _context.CrateMembers
                    .Where(m => batch.Contains(m.CrateId) && m.UserId == userId)
                    .ToListAsync();

                var leavableMemberships = memberships
                    .Where(m => m.Role != CrateRole.Owner)
                    .ToList();

                if (leavableMemberships.Any())
                {
                    _context.CrateMembers.RemoveRange(leavableMemberships);
                    await _context.SaveChangesAsync();
                    totalLeftCount += leavableMemberships.Count;

                    foreach (var membership in leavableMemberships)
                    {
                        _logger.LogInformation("User {UserId} left crate {CrateId}", 
                            userId, membership.CrateId);
                    }
                }

                var ownerMemberships = memberships
                    .Where(m => m.Role == CrateRole.Owner).ToList();

                foreach (var ownership in ownerMemberships)
                {
                    _logger.LogWarning("User {UserId} attempted to leave crate {CrateId} but is the owner",
                        userId, ownership.CrateId);
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process batch for user {UserId}", userId);
                return Result<int>.Failure(new InternalError($"Failed to leave crates: {ex.Message}"));
            }

            crateIdList = crateIdList.Skip(BatchSize).ToList();
        }

        return Result<int>.Success(totalLeftCount);
    }
}