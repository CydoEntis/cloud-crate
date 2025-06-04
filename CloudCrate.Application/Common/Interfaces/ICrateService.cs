using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateService
{
    Task<CrateDto> CreateCrateAsync(string userId, string crateName);
    Task<IEnumerable<CrateDto>> GetAllCratesAsync(string userId);
    Task<CrateDto> RenameCrateAsync(Guid crateId, string userId, string newName);
    Task AddFileToCrateAsync(Guid crateId, string userId, FileObject file);

    Task UploadFileAsync(Guid crateId, string userId, UploadFileDto file);
    Task<(Stream FileStream, string FIleName)> DownloadFileAsync(Guid crateId, string userId, Guid fileId);
}