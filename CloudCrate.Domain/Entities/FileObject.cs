namespace CloudCrate.Domain.Entities;

public class FileObject
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public long SizeInBytes { get; set; }
    public string MimeType { get; set; }

    public Guid CrateId { get; set; }
    public Crate Crate { get; set; }

    public Guid? FolderId { get; set; }
    public Folder? Folder { get; set; }


    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static FileObject Create(string name, long sizeInBytes, string mimeType, Guid crateId, Guid? folderId = null,
        Guid? categoryId = null)
    {
        return new FileObject
        {
            Id = Guid.NewGuid(),
            Name = name,
            SizeInBytes = sizeInBytes,
            MimeType = mimeType,
            CrateId = crateId,
            FolderId = folderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}