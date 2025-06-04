namespace CloudCrate.Domain.Entities;

public class FileObject
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public string StoredName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Size { get; set; }

    public Guid CrateId { get; set; }
    public Crate Crate { get; set; } = null!;
}