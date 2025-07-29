using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public string Owner { get; set; } = null!;
    public CrateRole Role { get; set; }
    public long TotalStorageUsed { get; set; }
    public int MemberCount { get; set; }
    public List<CrateMemberResponse> FirstThreeMembers { get; set; }
}