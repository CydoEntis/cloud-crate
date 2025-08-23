namespace CloudCrate.Application.DTOs;

public class MultipleMoveRequest
{
    public Guid CrateId { get; set; }
    public List<Guid> FolderIds { get; set; } = new();

    public List<Guid> FileIds { get; set; } = new();

    // null or Guid.Empty => move to root
    public Guid? NewParentId { get; set; }
}