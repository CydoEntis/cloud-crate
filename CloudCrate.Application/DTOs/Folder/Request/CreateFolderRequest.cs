namespace CloudCrate.Application.DTOs.Folder.Request;

public class CreateFolderRequest
{
    public string Name { get; set; } = default!;
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string Color { get; set; }
}