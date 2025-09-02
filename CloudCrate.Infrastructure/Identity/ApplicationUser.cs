using System.ComponentModel.DataAnnotations;
using CloudCrate.Application.Common.Constants;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)] public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)] public string ProfilePictureUrl { get; set; } = string.Empty;

    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;

    public long MaxStorageBytes
    {
        get => PlanStorageLimits.GetLimit(Plan);
    }

    public long UsedStorageBytes { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdadatedAt { get; set; } = DateTime.UtcNow;

    public static ApplicationUser Create(
        string email,
        string displayName = "",
        string profilePictureUrl = "",
        SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        return new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            ProfilePictureUrl = profilePictureUrl,
            Plan = plan,
            UsedStorageBytes = 0,
            CreatedAt = DateTime.UtcNow,
            UpdadatedAt = DateTime.UtcNow
        };
    }
}