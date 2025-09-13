using CloudCrate.Domain.Enums;
using CloudCrate.Domain.Exceptions;

namespace CloudCrate.Domain.Entities;

public class CrateMember
{
    public Guid Id { get; private set; }
    public Guid CrateId { get; private set; }
    public Crate Crate { get; set; } = null!;

    public string UserId { get; private set; } = string.Empty;
    public UserAccount User { get; set; } = null!;
    public CrateRole Role { get; private set; }

    public DateTime JoinedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public bool IsOwner => Role == CrateRole.Owner;
    public TimeSpan MembershipDuration => DateTime.UtcNow - JoinedAt;
    public bool IsRecentMember => MembershipDuration < TimeSpan.FromDays(7);

    public bool CanUpload => Role is CrateRole.Owner or CrateRole.Contributor or CrateRole.Uploader;

    public bool CanDownload =>
        Role is CrateRole.Owner or CrateRole.Contributor or CrateRole.Uploader or CrateRole.Downloader;

    public bool CanEdit => Role is CrateRole.Owner or CrateRole.Contributor;
    public bool CanManageMembers => Role == CrateRole.Owner;
    public bool CanManageSettings => Role == CrateRole.Owner;
    public bool CanDelete => Role == CrateRole.Owner;
    public bool CanView => true;

    private CrateMember()
    {
    }

    internal CrateMember(Guid id, Guid crateId, string userId, CrateRole role, DateTime joinedAt, DateTime updatedAt)
    {
        Id = id;
        CrateId = crateId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        UpdatedAt = updatedAt;
    }

    public static CrateMember Rehydrate(Guid id, Guid crateId, string userId, CrateRole role, DateTime joinedAt,
        DateTime updatedAt)
    {
        return new CrateMember(id, crateId, userId, role, joinedAt, updatedAt);
    }

    public static CrateMember Create(Guid crateId, string userId, CrateRole role)
    {
        if (crateId == Guid.Empty)
            throw new ValueEmptyException(nameof(crateId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValueEmptyException(nameof(userId));

        return new CrateMember
        {
            Id = Guid.NewGuid(),
            CrateId = crateId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public bool CanUpdateRole(CrateRole newRole, CrateMember requestingMember)
    {
        if (!requestingMember.IsOwner)
            return false;

        if (IsOwner && requestingMember.UserId == UserId && newRole != CrateRole.Owner)
        {
            return false;
        }

        return true;
    }

    public void UpdateRole(CrateRole newRole, CrateMember requestingMember)
    {
        if (!CanUpdateRole(newRole, requestingMember))
            throw new DomainValidationException("Insufficient permissions to change role");

        if (Role == newRole) return;

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public void PromoteToOwner(CrateMember requestingMember)
    {
        if (!requestingMember.IsOwner)
            throw new DomainValidationException("Only owners can promote members");

        if (IsOwner)
            throw new DomainValidationException("Member is already an owner");

        Role = CrateRole.Owner;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DemoteFromOwner(CrateMember requestingMember, CrateRole newRole = CrateRole.Contributor)
    {
        if (!requestingMember.IsOwner)
            throw new DomainValidationException("Only owners can demote members");

        if (!IsOwner)
            throw new DomainValidationException("Member is not an owner");

        if (requestingMember.UserId == UserId)
            throw new DomainValidationException("Cannot demote yourself as the only owner");

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanBeRemoved(CrateMember requestingMember)
    {
        if (IsOwner)
            return false;

        if (!requestingMember.IsOwner)
            return false;

        return true;
    }

    public bool CanLeave()
    {
        return !IsOwner;
    }

    public void ValidateRemoval(CrateMember requestingMember)
    {
        if (!CanBeRemoved(requestingMember))
        {
            if (IsOwner)
                throw new DomainValidationException("Owners cannot be removed. Transfer ownership first.");
            else
                throw new DomainValidationException("Only owners can remove members");
        }
    }

    public void ValidateLeaving()
    {
        if (!CanLeave())
            throw new DomainValidationException("Owners cannot leave the crate. Transfer ownership first.");
    }
}