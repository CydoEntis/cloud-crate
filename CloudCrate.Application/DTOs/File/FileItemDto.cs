namespace CloudCrate.Application.DTOs.File;

public class FileItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string MimeType { get; set; }
    public long SizeInBytes { get; set; }
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string? ParentFolderName { get; set; }
    public string UploadedByUserId { get; set; }
    public string UploadedByDisplayName { get; set; }
    public string UploadedByEmail { get; set; }
    public string UploadedByProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FileUrl { get; set; }
}