namespace CloudCrate.Application.DTOs.File;

public class SoftDeletedFile
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public long SizeInBytes { get; set; }
    public string? MimeType { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}