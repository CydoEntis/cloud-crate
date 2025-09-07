using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateService
{
    Task<Result<Guid>> CreateCrateAsync(CreateCrateRequest request);
    Task<Result<PaginatedResult<CrateListItemResponse>>> GetCratesAsync(CrateQueryParameters parameters);
    Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId);

    Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(
        Guid crateId,
        CrateMemberRequest request);

    Task<Result<CrateListItemResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName, string? newColor);
    Task<Result> DeleteCrateAsync(Guid crateId, string userId);
    Task<Result> LeaveCrateAsync(Guid crateId, string userId);
    Task<Result<bool>> AllocateCrateStorageAsync(string userId, Guid crateId, int requestedAllocationGB);


}