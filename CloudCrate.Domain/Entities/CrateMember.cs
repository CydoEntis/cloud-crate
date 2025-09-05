using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class CrateMember
{
    public Guid Id { get; set; }
    public Guid CrateId { get; set; }
    public Crate Crate { get; set; } = null!;

    public string UserId { get; set; } = string.Empty!;
    public CrateRole Role { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public static CrateMember Create(Guid crateId, string userId, CrateRole role)
    {
        return new CrateMember
        {
            Id = Guid.NewGuid(),
            CrateId = crateId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
    }
}