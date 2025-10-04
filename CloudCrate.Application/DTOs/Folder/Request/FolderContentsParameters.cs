using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Folder.Request;

public class FolderContentsParameters
{
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; } 
    public string? UserId { get; set; }
    public string? SearchTerm { get; set; }

    public OrderBy OrderBy { get; set; } = OrderBy.Name;
    public bool Ascending { get; set; } = true;

    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}