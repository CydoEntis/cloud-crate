namespace CloudCrate.Domain.Entities;

public class CrateFolder
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; } = "#EAAC00";

    public Guid? ParentFolderId { get; set; }
    public Folder? ParentFolder { get; set; }

    public Guid CrateId { get; set; }
    public Crate Crate { get; set; }

    public ICollection<FileObject> Files { get; set; } = new List<FileObject>();
    public ICollection<Folder> Subfolders { get; set; } = new List<Folder>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string UserId { get; set; }
    public CrateUser User { get; set; }

    public bool IsDeleted { get; set; } = false;

    public static CrateFolder Create(
        string name,
        Guid crateId,
        Guid? parentFolderId,
        string? color,
        string userId
    )
    {
        return new CrateFolder
        {
            Id = Guid.NewGuid(),
            Name = name,
            CrateId = crateId,
            ParentFolderId = parentFolderId,
            Color = color ?? "#EAAC00",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = userId,
            IsDeleted = false
        };
    }
}