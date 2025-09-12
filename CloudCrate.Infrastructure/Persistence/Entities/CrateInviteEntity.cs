using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;

namespace CloudCrate.Infrastructure.Persistence.Entities;

public class CrateInviteEntity
{
    public Guid Id { get; set; }
    public Guid CrateId { get; set; }
    public CrateEntity Crate { get; set; } = null!;

    public string InvitedUserEmail { get; set; } = null!;
    public string InvitedByUserId { get; set; } = null!;
    public ApplicationUser InvitedByUser { get; set; } = null!; 

    public CrateRole Role { get; set; }
    public string Token { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public InviteStatus Status { get; set; }
}