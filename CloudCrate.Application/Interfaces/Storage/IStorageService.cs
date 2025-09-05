using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Models;


namespace CloudCrate.Application.Interfaces.Storage;

public interface IStorageService
{
    Task<Result> CreateBucketAsync(string bucketName);
    Task<Result<bool>> BucketExistsAsync(string bucketName);
    Task<Result> GetOrCreateBucketAsync(string bucketName);


    Task<Result<string>> GetFileUrlAsync(string userId, Guid crateId, Guid? folderId, string fileName,
        TimeSpan? expiry = null);

    Task<Result<string>> SaveFileAsync(string userId, FileUploadRequest request);
    Task<Result<List<string>>> SaveFilesAsync(string userId, List<FileUploadRequest> requests);

    Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    bool FileExists(string userId, Guid crateId, Guid? folderId, string fileName);

    Task<Result> DeleteAllFilesInBucketAsync(Guid crateId);
    Task<Result> DeleteBucketAsync(Guid crateId);
}