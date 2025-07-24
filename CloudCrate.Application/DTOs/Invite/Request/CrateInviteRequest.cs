using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Invite.Request;

public class CrateInviteRequest
{
    public Guid CrateId { get; set; }
    public string Email { get; set; } = string.Empty!;

    public CrateRole Role { get; set; }

    public DateTime? ExpiresAt { get; set; }
}