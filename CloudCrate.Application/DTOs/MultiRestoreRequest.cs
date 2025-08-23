namespace CloudCrate.Application.DTOs;

public class MultipleRestoreRequest
{
    public Guid CrateId { get; set; }
    public List<Guid> FolderIds { get; set; } = new();
    public List<Guid> FileIds { get; set; } = new();
}