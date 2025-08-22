namespace CloudCrate.Application.DTOs.Folder.Request;

public class UpdateFolderRequest
{
    public Guid FolderId { get; set; }
    public string? NewName { get; set; }
    public string? NewColor { get; set; }
}