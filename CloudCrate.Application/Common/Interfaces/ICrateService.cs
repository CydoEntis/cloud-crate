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
    Task<Result<string>> UploadFileAsync(string userId, Guid crateId, FileDataDto file);
    Task<Result<DownloadedFileDto>> DownloadFileAsync(string userId, Guid crateId, Guid fileId);
    Task<Result<IEnumerable<StoredFileDto>>> GetFilesInCrateAsync(Guid crateId, string userId);
    Task<Result> DeleteFileAsync(Guid crateId, string userId, Guid fileId);
}