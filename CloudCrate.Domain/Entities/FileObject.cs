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


    public Guid? CategoryId { get; set; }
}