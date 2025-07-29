using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateMemberResponse
{
    public string UserId { get; set; }
    public string Email { get; set; }
    public string DisplayName { get; set; }
    public CrateRole Role { get; set; }
    public string ProfilePicture { get; set; }
    public DateTime JoinedAt { get; set; }
}