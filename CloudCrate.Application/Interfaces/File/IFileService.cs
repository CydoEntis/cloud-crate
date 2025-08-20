using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
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

    Task<PaginatedResult<FileItemDto>> GetFilesAsync(GetFilesParameters parameters);
}