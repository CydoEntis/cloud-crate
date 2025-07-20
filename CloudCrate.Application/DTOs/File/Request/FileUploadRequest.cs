namespace CloudCrate.Application.DTOs.File.Request;

public class FileUploadRequest
{
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; }
    public string FileName { get; set; } = default!;
    public string MimeType { get; set; } = default!;
    public long SizeInBytes { get; set; }
    public Stream Content { get; set; } = default!;
}