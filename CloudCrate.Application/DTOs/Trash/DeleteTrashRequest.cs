namespace CloudCrate.Application.DTOs.Trash;

public class DeleteTrashRequest
{
    public List<Guid> FileIds { get; set; } = new();
    public List<Guid> FolderIds { get; set; } = new();
}