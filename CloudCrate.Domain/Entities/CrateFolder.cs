namespace CloudCrate.Domain.Entities;

public class CrateFolder
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; } = "#EAAC00";

    public Guid? ParentFolderId { get; set; }
    public CrateFolder? ParentFolder { get; set; }

    public bool IsRoot { get; private set; }
    public Guid CrateId { get; set; }
    public Crate Crate { get; set; }

    public ICollection<CrateFile> Files { get; set; } = new List<CrateFile>();
    public ICollection<CrateFolder> Subfolders { get; set; } = new List<CrateFolder>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string UserId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    public string? DeletedByUserId { get; set; }
    public string? RestoredByUserId { get; set; }
    public DateTime? RestoredAt { get; set; }

    public static CrateFolder CreateRoot(
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
            Name = "Root",
            CrateId = crateId,
            ParentFolderId = parentFolderId,
            IsRoot = true,
            Color = color ?? "#EAAC00",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = userId,
            IsDeleted = false
        };
    }
    
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
            IsRoot = false,
            Color = color ?? "#EAAC00",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = userId,
            IsDeleted = false
        };
    }
}