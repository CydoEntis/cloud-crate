using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class CrateInvite
{
    public Guid Id { get; private set; }
    public Guid CrateId { get; private set; }
    public Crate Crate { get; set; }

    public string InvitedUserEmail { get; private set; } = null!;
    public string InvitedByUserId { get; private set; } = null!;
    public CrateRole Role { get; private set; }

    public string Token { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public InviteStatus Status { get; private set; }

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
            UpdatedAt = null,
            Status = InviteStatus.Pending
        };
    }

    public void UpdateInviteStatus(InviteStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}