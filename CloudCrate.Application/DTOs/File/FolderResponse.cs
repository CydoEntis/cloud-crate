namespace CloudCrate.Application.DTOs.File;

public class FolderResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
}