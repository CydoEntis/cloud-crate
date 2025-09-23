namespace CloudCrate.Application.DTOs.Invite.Response;

public class InviteUserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByUserId { get; set; }
    public bool IsUsed { get; set; }
    public bool IsExpired { get; set; }
    public bool IsValid { get; set; }
    public string InviteUrl { get; set; } = string.Empty;
    public string? Notes { get; set; }
}