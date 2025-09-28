using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces;

public interface IBatchMoveService
{
    Task<Result> MoveItemsAsync(List<Guid> fileIds, List<Guid> folderIds, Guid? newParentId, string userId);
}