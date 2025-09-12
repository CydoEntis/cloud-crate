using System.ComponentModel.DataAnnotations;
using CloudCrate.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)] public string DisplayName { get; set; } = string.Empty;
    [MaxLength(500)] public string ProfilePictureUrl { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public long UsedAccountStorageBytes { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}