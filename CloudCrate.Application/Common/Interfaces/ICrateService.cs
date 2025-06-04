using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateService
{
    Task<CrateDto> CreateCrateAsync(string userId, string crateName);
    Task<IEnumerable<CrateDto>> GetAllCratesAsync(string userId);
    Task<CrateDto> RenameCrateAsync(Guid crateId, string userId, string newName);
    Task AddFileToCrateAsync(Guid crateId, string userId, FileObject file);
}