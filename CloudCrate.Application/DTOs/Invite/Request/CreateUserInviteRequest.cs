namespace CloudCrate.Application.DTOs.Invite.Request;

public class CreateUserInviteRequest
{
    public string? Email { get; set; }
    public int ExpiryHours { get; set; } = 168; 
}