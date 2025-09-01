using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Folder.Request;

public class FolderContentsParameters
{
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; } // maps to FolderId for files
    public string? UserId { get; set; }
    public string? SearchTerm { get; set; }

    // Sorting
    public OrderBy OrderBy { get; set; } = OrderBy.Name;
    public bool Ascending { get; set; } = true;

    // Optional filters
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }

    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}