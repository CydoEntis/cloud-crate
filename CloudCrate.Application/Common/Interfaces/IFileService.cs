using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Interfaces;

public interface IFileService
{
    Task<Result<List<Folder>>> GetFoldersAsync(Guid crateId, string userId);

    Task<Result<List<FileObject>>> GetFilesInCrateRootAsync(Guid crateId, string userId);

    Task<Result<List<FileObject>>> GetFilesInFolderAsync(Guid folderId, string userId);

    Task<Result<FileObject>> UploadFileAsync(FileUploadRequest request, string userId);

    Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId);

    Task<Result> DeleteFileAsync(Guid fileId, string userId);

    Task<Result<FileObject>> GetFileByIdAsync(Guid fileId, string userId);
}