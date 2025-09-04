namespace CloudCrate.Api.DTOs.File.Request;

public class UploadMultipleFilesRequest
{
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; }
    public List<IFormFile> Files { get; set; } = new();
}