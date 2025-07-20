namespace CloudCrate.Application.DTOs.Folder.Response;

public class FolderResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string Color { get; set; }
}