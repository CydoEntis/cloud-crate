using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface ICrateService
{
    Task<Result<CrateResponse>> CreateCrateAsync(string userId, string crateName);
    Task<Result<IEnumerable<CrateResponse>>> GetAllCratesAsync(string userId);
    Task<Result<CrateResponse>> RenameCrateAsync(string userId, RenameCrateRequest request);
    Task<Result<string>> AddFileToCrateAsync(string userId, AddFileToCrateRequest request);
    Task<Result<string>> UploadFileAsync(string userId, FileDataRequest dataRequest);
    Task<Result<DownloadFileResponse>> DownloadFileAsync(string userId, DownloadFileRequest request);

    Task<Result<IEnumerable<FileObjectResponse>>> GetFilesInCrateAsync(Guid crateId, string userId);
    Task<Result> DeleteFileAsync(Guid crateId, string userId, Guid fileId);
}