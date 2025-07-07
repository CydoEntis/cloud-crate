using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Folder;

namespace CloudCrate.Application.Common.Interfaces;

public interface IFolderService
{
    Task<Result<FolderResponse>> CreateFolderAsync(CreateFolderRequest request, string userId);
    Task<Result> RenameFolderAsync(Guid folderId, string newName, string userId);
    Task<Result> DeleteFolderAsync(Guid folderId, string userId);
    Task<Result<List<FolderResponse>>> GetRootFoldersAsync(Guid crateId, string userId);
    Task<Result<List<FolderResponse>>> GetSubfoldersAsync(Guid parentId, string userId);
    Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId);
    
    Task<Result<FolderContentsResponse>> GetFolderContentsAsync(
        Guid crateId,
        Guid? parentFolderId,
        string userId,
        string? search = null,
        int page = 1,
        int pageSize = 20
    );
}