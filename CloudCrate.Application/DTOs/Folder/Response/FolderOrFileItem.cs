using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Folder.Response;

public class FolderOrFileItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FolderItemType Type { get; set; } // Folder or File
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string? Color { get; set; } // Only for folders
    public long SizeInBytes { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public string UploadedByDisplayName { get; set; } = "Unknown";
    public string UploadedByEmail { get; set; } = string.Empty;
    public string UploadedByProfilePictureUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string? MimeType { get; set; }
}