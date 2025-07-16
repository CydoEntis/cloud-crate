using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class CrateInvite
{
    public Guid Id { get; set; }
        
    public Guid CrateId { get; set; }
    public Crate Crate { get; set; }

    public string InvitedUserEmail { get; set; } = null!; 
        
    public string InvitedByUserId { get; set; } = null!; 
        
    public CrateRole Role { get; set; }

    public string Token { get; set; } = null!; 
        
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    public DateTime? ExpiresAt { get; set; }

    public InviteStatus Status { get; set; } = InviteStatus.Pending;
}