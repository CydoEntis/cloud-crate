namespace CloudCrate.Application.DTOs.File;

public class FileDataDto
{
    public Guid CrateId { get; set; }
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
}