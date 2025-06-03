using CloudCrate.Application.DTOs.Crate;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateService
{
    Task<CrateDto> CreateCrateAsync(string userId, string crateName);
    Task<IEnumerable<CrateDto>> GetAllCratesAsync(string userId);
}