namespace CloudCrate.Application.DTOs.File;

public class FileObjectResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public string StoredName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Size { get; set; }
}