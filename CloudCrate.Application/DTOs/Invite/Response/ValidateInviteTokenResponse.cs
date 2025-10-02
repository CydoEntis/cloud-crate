namespace CloudCrate.Application.DTOs.Invite.Response;

public class ValidateInviteTokenResponse
{
    public bool IsValid { get; set; }
    public string? Email { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}