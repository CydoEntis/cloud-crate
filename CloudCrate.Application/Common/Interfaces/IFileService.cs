using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;

namespace CloudCrate.Application.Common.Interfaces;

public interface IFileService
{
    Task<Result<FileDto>> UploadFileAsync(string userId, Guid crateId, FileDto fileData);
    Task<Result<FileDto>> DownloadFileAsync(string userId, Guid crateId, Guid fileId);
    Task<Result<IEnumerable<FileDto>>> GetFilesInCrateAsync(string userId, Guid crateId);
    Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid fileId);
}