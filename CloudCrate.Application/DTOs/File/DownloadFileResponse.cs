namespace CloudCrate.Application.DTOs.File;

public class DownloadFileResponse
{
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = "application/octet-stream";
}