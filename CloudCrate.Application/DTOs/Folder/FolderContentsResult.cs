using CloudCrate.Application.DTOs.Folder.Response;

namespace CloudCrate.Application.DTOs.Folder;

public class FolderContentsResult
{
    public string FolderName { get; set; } = "Root";
    public Guid? ParentFolderId { get; set; }

    public List<FolderOrFileItem> Items { get; set; } = new();
    public int TotalCount { get; set; } = 0;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public List<FolderBreadcrumb> Breadcrumbs { get; set; } = new();

    public static FolderContentsResult Create(
        List<FolderOrFileItem> items,
        int totalCount = 0,
        int page = 1,
        int pageSize = 20,
        string folderName = "Root",
        Guid? parentFolderId = null,
        List<FolderBreadcrumb>? breadcrumbs = null)
    {
        return new FolderContentsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            FolderName = folderName,
            ParentFolderId = parentFolderId,
            Breadcrumbs = breadcrumbs ?? new List<FolderBreadcrumb>()
        };
    }
}