using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Storage;

public interface IStorageService
{
    Task<Result> EnsureBucketExistsAsync();
    Task<Result<string>> SaveFileAsync(FileUploadRequest request);
    Task<Result<List<string>>> SaveFilesAsync(List<FileUploadRequest> requests);
    Task<Result<byte[]>> ReadFileAsync(Guid crateId, Guid? folderId, string fileName);
    Task<bool> FileExistsAsync(Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFileAsync(Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFilesAsync(Guid crateId, Guid? folderId, IEnumerable<string> fileNames);
    Task<Result> DeleteAllFilesForCrateAsync(Guid crateId);
    Task<Result<string>> GetFileUrlAsync(Guid crateId, Guid? folderId, string fileName, TimeSpan? expiry = null);
    Task<Result> MoveFileAsync(Guid crateId, Guid? oldFolderId, Guid? newFolderId, string fileName);
    Task<Result> MoveFolderAsync(Guid crateId, Guid folderId, Guid? newParentId);
    Task<Result> RenameFileAsync(Guid crateId, Guid? folderId, string oldFileName, string newFileName);
}