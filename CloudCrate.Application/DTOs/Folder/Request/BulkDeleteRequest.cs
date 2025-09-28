namespace CloudCrate.Application.DTOs.Folder.Request;

public class BulkDeleteRequest
{
    public List<Guid>? FileIds { get; set; }
    public List<Guid>? FolderIds { get; set; }
}