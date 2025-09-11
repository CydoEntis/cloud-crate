using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services;

public class BatchMembershipService : IBatchMembershipService
{
    private readonly IAppDbContext _context;
    private readonly ICrateRoleService _crateRoleService;
    private readonly ILogger<BatchMembershipService> _logger;
    private const int BatchSize = 500;

    public BatchMembershipService(
        IAppDbContext context,
        ICrateRoleService crateRoleService,
        ILogger<BatchMembershipService> logger)
    {
        _context = context;
        _crateRoleService = crateRoleService;
        _logger = logger;
    }

    public async Task<Result<int>> LeaveCratesAsync(string userId, IEnumerable<Guid> crateIds)
    {
        var list = crateIds.ToList();
        var leftCount = 0;

        while (list.Any())
        {
            var batch = list.Take(BatchSize).ToList();

            foreach (var crateId in batch)
            {
                var membership = await _context.CrateMembers
                    .FirstOrDefaultAsync(m => m.CrateId == crateId && m.UserId == userId);

                if (membership != null)
                {
                    if (await _crateRoleService.IsOwner(crateId, userId))
                    {
                        _logger.LogWarning(
                            "User {UserId} attempted to leave crate {CrateId} but is the owner",
                            userId, crateId);
                        continue;
                    }

                    _context.CrateMembers.Remove(membership);
                    leftCount++;
                    _logger.LogInformation("User {UserId} left crate {CrateId}", userId, crateId);
                }
                else
                {
                    _logger.LogWarning(
                        "User {UserId} is not a member of crate {CrateId}", userId, crateId);
                }
            }

            await _context.SaveChangesAsync();
            list = list.Skip(BatchSize).ToList();
        }

        return Result<int>.Success(leftCount);
    }
}