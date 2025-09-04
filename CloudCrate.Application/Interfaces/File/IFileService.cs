using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Interfaces.File;

public interface IFileService
{
    // Fetch single file info (with URL)
    Task<Result<CrateFileResponse>> FetchFileResponseAsync(Guid fileId, string userId);

    Task<PaginatedResult<CrateFileResponse>> FetchFilesAsync(FolderContentsParameters parameters);

    Task<long> FetchTotalFileSizeInFolderAsync(Guid folderId);

    Task<List<CrateFile>> FetchFilesInFolderRecursivelyAsync(Guid folderId);

    Task<Result<byte[]>> FetchFileBytesAsync(Guid fileId, string userId);

    Task<Result<Guid>> UploadFileAsync(FileUploadRequest request, string userId);

    Task<Result<List<Guid>>> UploadFilesAsync(MultiFileUploadRequest request, string userId);

    Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId);

    Task<Result> DeleteFileAsync(Guid fileId, string userId);

    Task<Result> SoftDeleteFileAsync(Guid fileId, string userId);
    Task<Result> SoftDeleteFilesAsync(List<Guid> fileIds, string userId);

    Task<Result> PermanentlyDeleteFilesAsync(List<Guid> fileIds, string userId);

    Task<Result> DeleteFilesInFolderRecursivelyAsync(Guid folderId, string userId);

    Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId);
    Task<Result> MoveFilesAsync(IEnumerable<Guid> fileIds, Guid? newParentId, string userId);

    Task<Result> RestoreFileAsync(Guid fileId, string userId);
    Task<Result> RestoreFilesAsync(List<Guid> fileIds, string userId);

}
