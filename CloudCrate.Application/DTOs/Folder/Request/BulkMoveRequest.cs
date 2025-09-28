namespace CloudCrate.Application.DTOs.Folder.Request;

public class BulkMoveRequest
{
    public List<Guid>? FileIds { get; set; }
    public List<Guid>? FolderIds { get; set; }
    public Guid? NewParentId { get; set; }
}