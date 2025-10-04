using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.Trash;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Folder;

public interface IFolderService
{
    Task<Result<FolderContentsResponse>> GetFolderContentsAsync(FolderContentsParameters parameters);
    Task<Result<Guid>> CreateFolderAsync(CreateFolderRequest request, string userId);
    Task<Result> UpdateFolderAsync(Guid folderId, UpdateFolderRequest request, string userId);

    Task<Result> SoftDeleteFolderAsync(Guid folderId, string userId);
    Task<Result> PermanentlyDeleteFolderAsync(Guid folderId, string userId);
    Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId);
    Task<Result> RestoreFolderAsync(Guid folderId, string userId);

    Task<Result> BulkMoveItemsAsync(List<Guid> fileIds, List<Guid> folderIds, Guid? newParentId, string userId);

    Task<Result> BulkSoftDeleteItemsAsync(List<Guid> fileIds, List<Guid> folderIds, string userId);

    Task<Result> EmptyTrashAsync(Guid crateId, string userId);
    Task<Result<FolderDownloadResult>> DownloadFolderAsync(Guid folderId, string userId);

    Task<Result<PaginatedResult<FolderResponse>>> GetAvailableMoveFoldersAsync(GetAvailableMoveTargetsRequest request,
        string userId);

    Task<Result<PaginatedResult<TrashItemResponse>>> GetTrashItemsAsync(
        TrashQueryParameters parameters);
}