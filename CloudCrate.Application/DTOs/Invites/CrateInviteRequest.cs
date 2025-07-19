using System.ComponentModel.DataAnnotations;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Invites;

public class CrateInviteRequest
{
    [Required] 
    public string Email { get; set; } = string.Empty!;

    [Required]
    public CrateRole Role { get; set; }

    public DateTime? ExpiresAt { get; set; }
}