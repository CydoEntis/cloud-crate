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

    private CrateInvite()
    {
    }

    internal CrateInvite(Guid id, Guid crateId, string invitedUserEmail, string invitedByUserId, CrateRole role,
        string token,
        DateTime createdAt, DateTime? expiresAt, DateTime? updatedAt, InviteStatus status)
    {
        Id = id;
        CrateId = crateId;
        InvitedUserEmail = invitedUserEmail;
        InvitedByUserId = invitedByUserId;
        Role = role;
        Token = token;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        UpdatedAt = updatedAt;
        Status = status;
    }

    public static CrateInvite Rehydrate(Guid id, Guid crateId, string invitedUserEmail, string invitedByUserId,
        CrateRole role,
        string token, DateTime createdAt, DateTime? expiresAt, DateTime? updatedAt, InviteStatus status)
    {
        return new CrateInvite(id, crateId, invitedUserEmail, invitedByUserId, role, token, createdAt, expiresAt,
            updatedAt, status);
    }

    public static CrateInvite Create(Guid crateId, string email, string invitedByUserId, CrateRole role,
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
    
    public void UpdateInviteStatus(InviteStatus newStatus)
    {
        if (Status == newStatus)
            return;

        if (Status != InviteStatus.Pending)
            throw new InvalidOperationException("Cannot change status of a processed invite.");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

}