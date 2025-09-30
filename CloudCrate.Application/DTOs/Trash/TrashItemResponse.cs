namespace CloudCrate.Application.DTOs.Trash;

public class TrashItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TrashItemType Type { get; set; }

    public long? SizeInBytes { get; set; }

    public DateTime DeletedAt { get; set; }

    public string DeletedByUserId { get; set; } = string.Empty;
    public string DeletedByUserName { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;

    public bool CanRestore { get; set; }
    public bool CanPermanentlyDelete { get; set; }
}

public enum TrashItemType
{
    File,
    Folder
}