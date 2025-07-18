using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateService
{
    Task<Result<bool>> CanCreateCrateAsync(string userId);
    Task<Result<int>> GetCrateCountAsync(string userId);
    Task<Result<long>> GetTotalUsedStorageAsync(string userId);
    Task<Result<Crate>> CreateCrateAsync(string userId, string name, string color);
    Task<Result<CrateResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName, string? newColor);
    Task<Result> DeleteCrateAsync(Guid crateId, string userId);
    Task<Result<List<Crate>>> GetCratesAsync(string userId);
    Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId);
}