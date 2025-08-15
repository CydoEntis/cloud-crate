public class FolderResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string Color { get; set; }

    public string? UploadedByUserId { get; set; }
    public string? UploadedByDisplayName { get; set; }
    public string? UploadedByEmail { get; set; }
    public string? UploadedByProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}