using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.Pagination;

namespace CloudCrate.Application.DTOs.Folder;

public class FolderContentsResult : PaginatedResult<FolderOrFileItem>
{
    public string FolderName { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }

    public static FolderContentsResult Create(
        List<FolderOrFileItem> items, int totalCount, int page, int pageSize,
        string folderName = "Root", Guid? parentFolderId = null)
    {
        return new FolderContentsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            FolderName = folderName,
            ParentFolderId = parentFolderId
        };
    }
}