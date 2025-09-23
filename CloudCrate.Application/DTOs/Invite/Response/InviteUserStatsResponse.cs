namespace CloudCrate.Application.DTOs.Invite.Response;

public class InviteUserStatsResponse
{
    public int TotalInvites { get; set; }
    public int UsedInvites { get; set; }
    public int ExpiredInvites { get; set; }
    public int ActiveInvites { get; set; }
    public DateTime? LastInviteCreated { get; set; }
    public DateTime? LastInviteUsed { get; set; }
}