using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Models;


namespace CloudCrate.Application.Interfaces.Storage;

public interface IStorageService
{
    Task<Result> CreateBucketAsync(string bucketName);
    Task<Result<bool>> BucketExistsAsync(string bucketName);
    Task<Result> GetOrCreateBucketAsync(string bucketName);

    Task<Result> DeleteBucketAsync(Guid crateId);
    Task<Result> DeleteBucketsAsync(IEnumerable<Guid> crateIds);
    Task<Result> DeleteAllFilesInBucketAsync(Guid crateId);

    Task<Result<string>> GetFileUrlAsync(string userId, Guid crateId, Guid? folderId, string fileName,
        TimeSpan? expiry = null);

    Task<Result<string>> SaveFileAsync(string userId, FileUploadRequest request);
    Task<Result<List<string>>> SaveFilesAsync(string userId, List<FileUploadRequest> requests);
    Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFilesAsync(string userId, Guid crateId, Guid? folderId, IEnumerable<string> fileNames);
    Task<bool> FileExistsAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFolderAsync(string userId, Guid crateId, Guid folderId);
    Task<Result> DeleteFoldersAsync(string userId, Guid crateId, IEnumerable<Guid> folderIds);
}