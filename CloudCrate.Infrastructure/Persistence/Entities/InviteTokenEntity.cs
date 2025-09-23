using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudCrate.Infrastructure.Persistence.Entities;

[Table("InviteTokens")]
public class InviteTokenEntity
{
    [Key] [MaxLength(36)] public string Id { get; set; } = string.Empty;

    [Required] [MaxLength(100)] public string Token { get; set; } = string.Empty;

    [Required] [MaxLength(36)] public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(256)] public string? Email { get; set; }

    [Required] public DateTime CreatedAt { get; set; }

    [Required] public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    [MaxLength(36)] public string? UsedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))] public ApplicationUser? CreatedByUser { get; set; }

    [ForeignKey(nameof(UsedByUserId))] public ApplicationUser? UsedByUser { get; set; }
}