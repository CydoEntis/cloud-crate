using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface IFileService
{
    Task<Result<List<FolderResponse>>> GetFoldersAsync(Guid crateId, string userId);

    Task<Result<List<FileObjectResponse>>> GetFilesInCrateRootAsync(Guid crateId, string userId);

    Task<Result<List<FileObjectResponse>>> GetFilesInFolderAsync(Guid crateId, Guid folderId, string userId);


    Task<Result<FileObjectResponse>> UploadFileAsync(FileUploadRequest request, string userId);

    Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId);

    Task<Result> DeleteFileAsync(Guid fileId, string userId);

    Task<Result<FileObjectResponse>> GetFileByIdAsync(Guid fileId, string userId);
}