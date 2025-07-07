using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Folder;

public class FolderOrFileItem
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public FolderItemType Type { get; set; }
    public string? MimeType { get; set; }
    public long? SizeInBytes { get; set; }
    public Guid CrateId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string? Color { get; set; }
}