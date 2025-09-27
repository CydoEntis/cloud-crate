using CloudCrate.Application.DTOs;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Folder;

public interface IFolderService
{
    Task<Result<Guid>> CreateFolderAsync(CreateFolderRequest request, string userId);

    Task<Result> UpdateFolderAsync(Guid folderId, UpdateFolderRequest request, string userId);

    Task<Result> DeleteFolderAsync(Guid folderId, string userId);
    Task<Result> PermanentlyDeleteFolderAsync(Guid folderId, string userId);

    Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId);

    Task<Result<FolderContentsResponse>> GetFolderContentsAsync(FolderContentsParameters parameters);

    Task<Result<FolderDownloadResult>> DownloadFolderAsync(Guid folderId, string userId);

    Task<Result> DeleteMultipleAsync(MultipleDeleteRequest request, string userId);
    Task<Result> SoftDeleteFolderAsync(Guid folderId, string userId);

    Task<Result<PaginatedResult<FolderResponse>>> GetAvailableMoveFoldersAsync(
        GetAvailableMoveTargetsRequest request,
        string userId);

    Task<Result> RestoreFolderAsync(Guid folderId, string userId);

    Task<Result> EmptyTrashAsync(Guid crateId, string userId);
}