namespace CloudCrate.Application.DTOs.File;

public class FileObjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public long SizeInBytes { get; set; }
    public string MimeType { get; set; }
    public Guid CrateId { get; set; }
    public Guid? FolderId { get; set; }
    public Guid? CategoryId { get; set; }
}