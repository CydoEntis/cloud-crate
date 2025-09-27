using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Interfaces.File;

public interface IFileService
{
    Task<Result<Guid>> UploadFileAsync(FileUploadRequest request, string userId);
    Task<Result<List<Guid>>> UploadFilesAsync(MultiFileUploadRequest request, string userId);

    Task<Result<CrateFileResponse>> GetFileAsync(Guid fileId, string userId);
    Task<Result<PaginatedResult<CrateFileResponse>>> GetFilesAsync(FolderContentsParameters parameters);
    Task<Result<List<CrateFile>>> GetFilesInFolderRecursivelyAsync(Guid folderId);
    Task<Result<long>> GetTotalFileSizeInFolderAsync(Guid folderId);

    Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId);
    Task<Result<byte[]>> DownloadMultipleFilesAsZipAsync(List<Guid> fileIds, string userId);

    Task<Result> DeleteFileAsync(Guid fileId, string userId);
    Task<Result> SoftDeleteFileAsync(Guid fileId, string userId);
    Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId);
    Task<Result> RestoreFileAsync(Guid fileId, string userId);

    Task<Result> SoftDeleteFilesAsync(List<Guid> fileIds, string userId);
    Task<Result> PermanentlyDeleteFilesAsync(List<Guid> fileIds, string userId);
    Task<Result> MoveFilesAsync(IEnumerable<Guid> fileIds, Guid? newParentId, string userId);
    Task<Result> RestoreFilesAsync(List<Guid> fileIds, string userId);
    Task<Result> UpdateFileAsync(Guid fileId, UpdateFileRequest request, string userId);
}