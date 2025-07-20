namespace CloudCrate.Application.DTOs.Folder.Request;

public class MoveFolderRequest
{
    public Guid FolderId { get; set; }
    public Guid? NewParentId { get; set; }
}