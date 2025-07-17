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
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public InviteStatus Status { get; set; }

    public static CrateInvite Create(
        Guid crateId,
        string email,
        string invitedByUserId,
        CrateRole role,
        DateTime? expiresAt = null)
    {
        return new CrateInvite
        {
            Id = Guid.NewGuid(),
            CrateId = crateId,
            InvitedUserEmail = email,
            InvitedByUserId = invitedByUserId,
            Role = role,
            Token = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(15),
            Status = InviteStatus.Pending
        };
    }
}