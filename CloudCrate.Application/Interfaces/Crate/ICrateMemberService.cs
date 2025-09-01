using CloudCrate.Application.Common.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateMemberService
{
    Task<List<Domain.Entities.Crate>> GetCratesForUserAsync(string userId);
    Task<DateTime?> GetUsersJoinDateAsync(Guid crateId, string userId);
    Task<CrateMember?> GetCrateOwnerAsync(Guid crateId);

    // Returns the CrateRole of the user
    Task<Result<CrateRole>> GetUserRoleAsync(Guid crateId, string userId);


    Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role);

    Task RemoveAllMembersFromCrateAsync(Guid crateId);
}