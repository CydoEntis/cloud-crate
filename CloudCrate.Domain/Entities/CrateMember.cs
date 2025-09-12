using CloudCrate.Domain.Enums;

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

    public void UpdateRole(CrateRole newRole)
    {
        if (Role == newRole) return;

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }
}