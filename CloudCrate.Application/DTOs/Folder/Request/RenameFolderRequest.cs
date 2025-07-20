namespace CloudCrate.Application.DTOs.Folder.Request;

public class RenameFolderRequest
{
    public Guid FolderId { get; set; }
    public string NewName { get; set; } = null!;
}