namespace CloudCrate.Domain.Entities;

public class StoredFile
{
    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}