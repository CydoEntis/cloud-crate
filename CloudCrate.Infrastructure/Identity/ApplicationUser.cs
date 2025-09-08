using System.ComponentModel.DataAnnotations;
using CloudCrate.Application.Constants;
using CloudCrate.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)] public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)] public string ProfilePictureUrl { get; set; } = string.Empty;

    public SubscriptionPlan Plan { get; private set; } = SubscriptionPlan.Free;

    public long AllocatedStorageLimitBytes => PlanStorageLimits.GetLimit(Plan);

    public long UsedAccountStorageBytes { get; private set; } = 0;

    public long RemainingAccountStorageBytes => AllocatedStorageLimitBytes - UsedAccountStorageBytes;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    protected ApplicationUser()
    {
    } // EF Core

    public static ApplicationUser Create(
        string email,
        string displayName = "",
        string profilePictureUrl = "",
        SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            ProfilePictureUrl = profilePictureUrl,
            Plan = plan,
            UsedAccountStorageBytes = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return user;
    }

    public void ChangePlan(SubscriptionPlan newPlan)
    {
        if (UsedAccountStorageBytes > PlanStorageLimits.GetLimit(newPlan))
            throw new InvalidOperationException("Cannot downgrade plan below current used storage.");

        Plan = newPlan;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConsumeStorage(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        if (UsedAccountStorageBytes + bytes > AllocatedStorageLimitBytes)
            throw new InvalidOperationException("Insufficient storage available.");

        UsedAccountStorageBytes += bytes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseStorage(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        UsedAccountStorageBytes = Math.Max(0, UsedAccountStorageBytes - bytes);
        UpdatedAt = DateTime.UtcNow;
    }
}