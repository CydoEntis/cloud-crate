using CloudCrate.Domain.Entities;

public class CrateFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;

    public Guid CrateId { get; set; }
    public Crate Crate { get; set; }

    public Guid? CrateFolderId { get; set; }
    public CrateFolder? CrateFolder { get; set; }

    public string UploaderId { get; set; } = string.Empty; 

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public static CrateFile Create(
        string name,
        long sizeInBytes,
        string mimeType,
        Guid crateId,
        string uploaderId,
        Guid? crateFolderId = null
    )
    {
        return new CrateFile
        {
            Id = Guid.NewGuid(),
            Name = name,
            SizeInBytes = sizeInBytes,
            MimeType = mimeType,
            CrateId = crateId,
            CrateFolderId = crateFolderId,
            UploaderId = uploaderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}