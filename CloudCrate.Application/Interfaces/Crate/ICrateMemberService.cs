using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateMemberService
{

    Task<CrateMember?> GetCrateMemberAsync(Guid crateId, string userId);

 
    Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role);

    Task RemoveAllMembersFromCrateAsync(Guid crateId);
}