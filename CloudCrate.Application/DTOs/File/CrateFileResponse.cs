using CloudCrate.Application.DTOs.User;

namespace CloudCrate.Application.DTOs.File;

public class CrateFileResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long SizeInBytes { get; set; } = 0;
    public string MimeType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; }

    public string? FolderName { get; set; } = null;
    public required Uploader Uploader { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsFolder { get; set; } = false;
}