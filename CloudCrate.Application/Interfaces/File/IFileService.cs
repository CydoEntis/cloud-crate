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

    // Fetch files with filtering, pagination, and ordering
    Task<PaginatedResult<CrateFileResponse>> FetchFilesAsync(FolderContentsParameters parameters);

    // Fetch total size of files in a folder
    Task<long> FetchTotalFileSizeInFolderAsync(Guid folderId);

    // Fetch all files recursively in a folder and its subfolders
    Task<List<CrateFile>> FetchFilesInFolderRecursivelyAsync(Guid folderId);

    // Fetch raw file bytes
    Task<Result<byte[]>> FetchFileBytesAsync(Guid fileId, string userId);

    // Upload a new file
    Task<Result<Guid>> UploadFileAsync(FileUploadRequest request, string userId);

    // Download a file
    Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId);

    // Delete a file permanently
    Task<Result> DeleteFileAsync(Guid fileId, string userId);

    // Soft delete (mark as deleted)
    Task<Result> SoftDeleteFileAsync(Guid fileId, string userId);
    Task<Result> SoftDeleteFilesAsync(List<Guid> fileIds, string userId);

    // Permanently delete multiple files
    Task<Result> PermanentlyDeleteFilesAsync(List<Guid> fileIds, string userId);

    // Delete all files in a folder recursively
    Task<Result> DeleteFilesInFolderRecursivelyAsync(Guid folderId, string userId);

    // Move files
    Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId);
    Task<Result> MoveFilesAsync(IEnumerable<Guid> fileIds, Guid? newParentId, string userId);

    // Restore files
    Task<Result> RestoreFileAsync(Guid fileId, string userId);
    Task<Result> RestoreFilesAsync(List<Guid> fileIds, string userId);


    // Optional: Get folder contents projection (commented out for now)
    // Task<List<FolderOrFileItem>> GetFilesForFolderContentsAsync(
    //     FolderQueryParameters parameters,
    //     bool searchMode,
    //     string? searchTerm = null
    // );
}
