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
    Task<Crate> CreateCrateAsync(string userId, string name);
    Task DeleteCrateAsync(Guid createId, string userId);
    Task<List<Crate>> GetCratesAsync(string userId);
}