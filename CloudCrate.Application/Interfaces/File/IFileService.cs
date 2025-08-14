using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Response;

namespace CloudCrate.Application.Interfaces.File;

public interface IFileService
{
    Task<long> GetTotalStorageUsedAsync(Guid crateId);
    Task<Result<List<FolderResponse>>> GetFoldersAsync(Guid crateId, string userId);

    Task<Result<FileObjectResponse>> UploadFileAsync(FileUploadRequest request, string userId);

    Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId);

    Task<Result> DeleteFileAsync(Guid fileId, string userId);

    Task<Result<FileObjectResponse>> GetFileByIdAsync(Guid fileId, string userId);

    Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId);
}