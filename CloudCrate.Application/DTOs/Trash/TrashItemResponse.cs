using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.DTOs.Trash;

public class TrashItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsFolder { get; set; }
    public DateTime DeletedAt { get; set; }

    public static TrashItemResponse FromFile(CrateFile file) => new()
    {
        Id = file.Id,
        Name = file.Name,
        IsFolder = false,
        DeletedAt = file.DeletedAt ?? file.CreatedAt
    };

    public static TrashItemResponse FromFolder(CrateFolder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        IsFolder = true,
        DeletedAt = folder.DeletedAt ?? folder.CreatedAt
    };
}