namespace CloudCrate.Application.DTOs.File.Response;

public class FileResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;
    public string UploadedByDisplayName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public string UploadedByProfilePictureUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}