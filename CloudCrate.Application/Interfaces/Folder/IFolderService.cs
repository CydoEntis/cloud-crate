using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.Pagination;

namespace CloudCrate.Application.Interfaces.Folder;

public interface IFolderService
{
    Task<Result<FolderResponse>> CreateFolderAsync(CreateFolderRequest request, string userId);
    Task<Result> RenameFolderAsync(Guid folderId, string newName, string userId);
    Task<Result> DeleteFolderAsync(Guid folderId, string userId);
    Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId);

    Task<Result<FolderContentsResult>> GetFolderContentsAsync(FolderQueryParameters parameters);
    
}