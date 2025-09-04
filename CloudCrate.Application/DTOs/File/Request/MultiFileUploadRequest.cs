namespace CloudCrate.Application.DTOs.File.Request;

public class MultiFileUploadRequest
{
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; }

    public List<FileUploadRequest> Files { get; set; } = new();
}