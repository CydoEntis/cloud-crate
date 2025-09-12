using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateService
{
    Task<Result<Guid>> CreateCrateAsync(CreateCrateRequest request);


    Task<Result<PaginatedResult<CrateListItemResponse>>> GetCratesAsync(CrateQueryParameters parameters);
    Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId);

    Task<Result> UpdateCrateAsync(Guid crateId, string userId, UpdateCrateRequest request);

    Task<Result> DeleteCrateAsync(Guid crateId, string userId);
    Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds, string userId);
}