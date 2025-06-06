using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateService
{
    Task<Result<CrateDto>> CreateCrateAsync(string userId, string crateName);
    Task<Result<IEnumerable<CrateDto>>> GetAllCratesAsync(string userId);
    Task<Result<CrateDto>> RenameCrateAsync(string userId, Guid crateId, string crateName);
}