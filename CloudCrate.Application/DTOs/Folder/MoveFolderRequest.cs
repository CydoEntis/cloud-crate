namespace CloudCrate.Application.DTOs.Folder;

public class MoveFolderRequest
{
    public Guid FolderId { get; set; }
    public Guid? NewParentId { get; set; }
}