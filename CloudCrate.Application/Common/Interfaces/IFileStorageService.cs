using CloudCrate.Application.Common.Models;

namespace CloudCrate.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<Result<string>> UploadAsync(Stream fileStream, string storedName);
    Task<Result<Stream>> DownloadAsync(string storedName);
    Task<Result> DeleteAsync(string storedName);
}