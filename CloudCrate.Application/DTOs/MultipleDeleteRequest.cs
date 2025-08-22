namespace CloudCrate.Application.DTOs;

public class MultipleDeleteRequest
{
    public List<Guid> FileIds { get; set; } = new();
    public List<Guid> FolderIds { get; set; } = new();
    public bool Permanent { get; set; }
}