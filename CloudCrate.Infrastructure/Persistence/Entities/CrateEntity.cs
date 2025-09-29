namespace CloudCrate.Infrastructure.Persistence.Entities;

public class CrateEntity
{
    private const string DefaultColor = "#4B9CED";
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = DefaultColor;

    public long AllocatedStorageBytes { get; set; }
    public long UsedStorageBytes { get; set; }
    public long RemainingStorageBytes => AllocatedStorageBytes - UsedStorageBytes;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DateTime LastAccessedAt {get; set;}
    public ICollection<CrateFolderEntity> Folders { get; set; } = new List<CrateFolderEntity>();
    public ICollection<CrateFileEntity> Files { get; set; } = new List<CrateFileEntity>();
    public ICollection<CrateMemberEntity> Members { get; set; } = new List<CrateMemberEntity>();
    public ICollection<CrateInviteEntity> Invites { get; set; } = new List<CrateInviteEntity>();
}