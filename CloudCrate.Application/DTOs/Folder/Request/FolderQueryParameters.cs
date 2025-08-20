using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Folder.Request;

public class FolderQueryParameters
{
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string? UserId { get; set; }
    public string? SearchTerm { get; set; }
    public FolderSortBy SortBy { get; set; } = FolderSortBy.Name;
    public OrderBy OrderBy { get; set; } = OrderBy.Asc;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}