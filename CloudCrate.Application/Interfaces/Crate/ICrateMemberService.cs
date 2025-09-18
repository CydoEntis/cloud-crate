using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.CrateMember.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Crate
{
    public interface ICrateMemberService
    {
        Task<CrateMember?> GetCrateMemberAsync(Guid crateId, string userId);

        Task<Result<PaginatedResult<CrateMemberResponse>>> GetCrateMembersAsync(
            Guid crateId,
            string requestingUserId,
            CrateMemberQueryParameters parameters);

        Task<Result> AssignRoleAsync(Guid crateId, string userId, CrateRole role, string requestingUserId);
        Task<Result> RemoveMemberAsync(Guid crateId, string userId, string requestingUserId);
        Task RemoveAllMembersFromCrateAsync(Guid crateId);

        Task<Result> LeaveCrateAsync(Guid crateId, string userId);
        Task<Result<int>> LeaveCratesAsync(IEnumerable<Guid> crateIds, string userId);
    }
}