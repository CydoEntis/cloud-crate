namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public CrateMemberResponse Owner { get; set; } = null!;
    public long UsedStorageBytes { get; set; }
    public long TotalStorageBytes { get; set; }
    public DateTime CratedAt { get; set; }
}