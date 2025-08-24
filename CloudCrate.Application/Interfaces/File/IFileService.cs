using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.Pagination;

namespace CloudCrate.Application.Interfaces.File;

public interface IFileService
{
    Task<Result<FileObjectResponse>> UploadFileAsync(FileUploadRequest request, string userId);

    Task<Result<byte[]>> DownloadFileAsync(Guid fileId, string userId);

    Task<Result> DeleteFileAsync(Guid fileId, string userId);

    Task<Result<FileObjectResponse>> GetFileByIdAsync(Guid fileId, string userId);

    Task<Result> MoveFileAsync(Guid fileId, Guid? newParentId, string userId);

    Task<Result> MoveFilesAsync(IEnumerable<Guid> fileIds, Guid? newParentId, string userId);

    Task<List<FileObject>> GetFilesInFolderRecursivelyAsync(Guid folderId);

    Task<Result<byte[]>> GetFileBytesAsync(Guid fileId, string userId);

    Task<long> GetFolderFilesSizeAsync(Guid folderId);

    Task<List<FolderOrFileItem>> GetFilesForFolderContentsAsync(
        FolderQueryParameters parameters,
        bool searchMode,
        string? searchTerm = null
    );

    Task<Result> SoftDeleteFileAsync(Guid fileId, string userId);

    Task<Result> SoftDeleteFilesAsync(List<Guid> fileIds, string userId);

    Task<Result> PermanentlyDeleteFilesAsync(List<Guid> fileIds, string userId);

    Task<PaginatedResult<FileItemDto>> GetFilesAsync(GetFilesParameters parameters);

    Task<long> GetFolderFilesSizeRecursiveAsync(Guid folderId);

    Task<Result> DeleteFilesInFolderRecursivelyAsync(Guid folderId, string userId);

    Task<Result> RestoreFileAsync(Guid fileId, string userId);
    
    Task<Result> RestoreFilesAsync(List<Guid> fileIds, string userId);
}