namespace CloudCrate.Application.DTOs.Folder;

public class FolderContentsResponse
{
    public List<FolderOrFileItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public Guid? ParentFolderId { get; set; } // ID of the current folder
    public Guid? ParentOfCurrentFolderId { get; set; } // ID of the folder above the current one
}