namespace CloudCrate.Application.DTOs.Folder.Response;

public class CrateFolderResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#EAAC00";
    public long SizeInBytes { get; set; } = 0;
    public Guid? ParentFolderId { get; set; }
    public string? ParentFolderName { get; set; }

    public Guid CrateId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
    public bool IsFolder { get; set; } = true;

    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string CreatedByProfilePictureUrl { get; set; } = string.Empty;
}