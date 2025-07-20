namespace CloudCrate.Api.DTOs.File.Request;

public class UploadFileRequest
{
    public IFormFile File { get; set; } = null!;
    public Guid? FolderId { get; set; }
}