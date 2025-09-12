using CloudCrate.Infrastructure.Identity;

namespace CloudCrate.Infrastructure.Persistence.Entities;

public class CrateFolderEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#EAAC00";

    public Guid? ParentFolderId { get; set; }
    public CrateFolderEntity? ParentFolder { get; set; }

    public bool IsRoot { get; set; } = false;
    public Guid CrateId { get; set; }
    public CrateEntity Crate { get; set; } = null!;

    public ICollection<CrateFileEntity> Files { get; set; } = new List<CrateFileEntity>();
    public ICollection<CrateFolderEntity> Subfolders { get; set; } = new List<CrateFolderEntity>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }

    public string? RestoredByUserId { get; set; }
    public ApplicationUser? RestoredByUser { get; set; }
    public DateTime? RestoredAt { get; set; }
}