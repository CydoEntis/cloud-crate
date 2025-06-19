namespace CloudCrate.Domain.Entities;

public class FileTag
{
    public Guid FileObjectId { get; set; }
    public FileObject FileObject { get; set; }

    public Guid TagId { get; set; }
    public Tag Tag { get; set; }
}