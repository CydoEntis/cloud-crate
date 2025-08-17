namespace CloudCrate.Application.DTOs.File.Response;

public class FileObjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public long SizeInBytes { get; set; }
    public string MimeType { get; set; }
    public string FileUrl { get; set; }
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; }

    public string UploadedByUserId { get; set; }
    public string UploadedByDisplayName { get; set; }
    public string UploadedByEmail { get; set; }
    public string UploadedByProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}