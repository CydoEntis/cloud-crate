using CloudCrate.Application.DTOs.Crate.Response;

namespace CloudCrate.Application.DTOs;

public class CrateMemberAvatarResponse
{
    public CrateMemberResponse Owner { get; set; } = null!;
    public List<CrateMemberResponse> RecentMembers { get; set; } = new();
    public int RemainingCount { get; set; }
}