using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class CrateUserRole
{
    public Guid Id { get; set; }
    public Guid CrateId { get; set; }
    public Crate Crate { get; set; } = null!;

    public string UserId { get; set; } = string.Empty!;

    public CrateRole Role { get; set; }

    public DateTime CreatedAt { get; set; }
}