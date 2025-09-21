namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public CrateMemberResponse Owner { get; set; } = null!;
    public long UsedStorageBytes { get; set; }
    public long AllocatedStorageBytes { get; set; }
    public DateTime JoinedAt { get; set; }
    public CrateRole CurrentUserRole { get; set; }
}