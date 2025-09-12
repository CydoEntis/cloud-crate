namespace CloudCrate.Domain.Entities;

public class CrateFolder
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#EAAC00";

    public Guid? ParentFolderId { get; private set; }
    public CrateFolder? ParentFolder { get; set; }

    public bool IsRoot { get; private set; }
    public Guid CrateId { get; private set; }
    public Crate Crate { get; set; }

    public ICollection<CrateFile> Files { get; private set; } = new List<CrateFile>();
    public ICollection<CrateFolder> Subfolders { get; private set; } = new List<CrateFolder>();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;
    public UserAccount CreatedByUser { get; set; } = null!;
    public bool IsDeleted { get; private set; } = false;
    public DateTime? DeletedAt { get; private set; }

    public string? DeletedByUserId { get; private set; }
    public UserAccount? DeletedByUser { get; private set; }

    public string? RestoredByUserId { get; private set; }
    public UserAccount? RestoredByUser { get; private set; }
    public DateTime? RestoredAt { get; private set; }

    private CrateFolder()
    {
    }

    internal CrateFolder(
        Guid id,
        string name,
        string color,
        Guid? parentFolderId,
        bool isRoot,
        Guid crateId,
        ICollection<CrateFile> files,
        ICollection<CrateFolder> subfolders,
        DateTime createdAt,
        DateTime updatedAt,
        string createdByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId,
        string? restoredByUserId,
        DateTime? restoredAt)
    {
        Id = id;
        Name = name;
        Color = color;
        ParentFolderId = parentFolderId;
        IsRoot = isRoot;
        CrateId = crateId;
        Files = files ?? new List<CrateFile>();
        Subfolders = subfolders ?? new List<CrateFolder>();
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedByUserId = createdByUserId;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
        DeletedByUserId = deletedByUserId;
        RestoredByUserId = restoredByUserId;
        RestoredAt = restoredAt;
    }

    public static CrateFolder Rehydrate(
        Guid id,
        string name,
        string color,
        Guid? parentFolderId,
        bool isRoot,
        Guid crateId,
        ICollection<CrateFile> files,
        ICollection<CrateFolder> subfolders,
        DateTime createdAt,
        DateTime updatedAt,
        string createdByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId,
        string? restoredByUserId,
        DateTime? restoredAt)
    {
        return new CrateFolder(
            id, name, color, parentFolderId, isRoot, crateId,
            files, subfolders, createdAt, updatedAt,
            createdByUserId, isDeleted, deletedAt, deletedByUserId, restoredByUserId, restoredAt
        );
    }

    public static CrateFolder CreateRoot(
        string name,
        Guid crateId,
        Guid? parentFolderId,
        string? color,
        string userId)
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
            CreatedByUserId = userId,
            IsDeleted = false
        };
    }

    public static CrateFolder Create(
        string name,
        Guid crateId,
        Guid? parentFolderId,
        string? color,
        string userId)
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
            CreatedByUserId = userId,
            IsDeleted = false
        };
    }
}