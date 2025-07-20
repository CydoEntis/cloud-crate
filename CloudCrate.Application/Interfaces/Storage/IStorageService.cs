using CloudCrate.Application.Common.Models;

namespace CloudCrate.Application.Interfaces.Storage;

public interface IStorageService
{
    Result EnsureDirectory(string userId, Guid crateId, Guid? folderId);
    Task<Result> SaveFileAsync(string userId, Guid crateId, Guid? folderId, string fileName, Stream content);
    Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    bool FileExists(string userId, Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFilesAsync(string bucketName, IEnumerable<string> objectKeys);
}