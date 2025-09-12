using CloudCrate.Infrastructure.Identity;

namespace CloudCrate.Infrastructure.Persistence.Entities;

public class CrateFileEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;

    public Guid CrateId { get; set; }
    public CrateEntity Crate { get; set; } = null!;

    public Guid? CrateFolderId { get; set; }
    public CrateFolderEntity? CrateFolder { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;
    public ApplicationUser UploadedByUser { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }
    public DateTime? RestoredAt { get; set; }

    public string? RestoredByUserId { get; set; }
    public ApplicationUser? RestoredByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}