using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.Trash;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Trash;

public interface ITrashService
{
    Task<PaginatedResult<TrashItemResponse>> FetchDeletedItemsAsync(Guid crateId, string userId, int page, int pageSize);

    Task<Result> RestoreItemsAsync(List<Guid> fileIds, List<Guid> folderIds, string userId);

    Task<Result> PermanentlyDeleteItemsAsync(List<Guid> fileIds, List<Guid> folderIds, string userId);
}