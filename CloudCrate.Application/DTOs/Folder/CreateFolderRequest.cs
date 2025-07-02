namespace CloudCrate.Application.DTOs.Folder;

public class CreateFolderRequest
{
    public string Name { get; set; } = default!;
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
}