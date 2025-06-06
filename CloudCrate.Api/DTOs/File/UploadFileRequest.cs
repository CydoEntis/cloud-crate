namespace CloudCrate.Api.Models;

public class UploadFileRequest
{
    public IFormFile File { get; set; } = null!;
}