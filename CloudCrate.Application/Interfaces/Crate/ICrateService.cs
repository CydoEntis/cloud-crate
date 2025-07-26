using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateService
{
    Task<Result<CrateResponse>> CreateCrateAsync(string userId, string name, string color);
    Task<Result<List<CrateResponse>>> GetCratesAsync(string userId);
    Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId);

    Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(
        Guid crateId,
        CrateMemberRequest request);

    Task<Result<CrateResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName, string? newColor);
    Task<Result> DeleteCrateAsync(Guid crateId, string userId);
}