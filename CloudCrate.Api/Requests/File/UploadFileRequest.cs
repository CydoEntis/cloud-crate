namespace CloudCrate.Api.Requests.File;

public class UploadFileRequest
{
    public IFormFile File { get; set; } = null!;
}