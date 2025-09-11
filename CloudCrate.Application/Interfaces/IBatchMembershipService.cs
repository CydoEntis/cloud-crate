using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces;

public interface IBatchMembershipService
{
    Task<Result<int>> LeaveCratesAsync(string userId, IEnumerable<Guid> crateIds);
}