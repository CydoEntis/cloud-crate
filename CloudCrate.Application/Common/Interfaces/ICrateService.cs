using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateService
{
    Task<bool> CanCreateCrateAsync(string userId);
    Task<int> GetCrateCountAsync(string userId);
    Task<long> GetTotalUsedStorageAsync(string userId);
    Task<Result<Crate>> CreateCrateAsync(string userId, string name, string color);
    Task<Result> DeleteCrateAsync(Guid crateId, string userId);
    Task<List<Crate>> GetCratesAsync(string userId);
}