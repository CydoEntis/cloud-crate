namespace CloudCrate.Application.DTOs.Invite.Request;

public class CrateInviteRequest
{
    public Guid CrateId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty!;

    public CrateRole Role { get; set; }
}