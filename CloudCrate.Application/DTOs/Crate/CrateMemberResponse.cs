using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate;

public class CrateMemberResponse
{
    public string UserId { get; set; }
    public string Email { get; set; }
    public CrateRole Role { get; set; }
}