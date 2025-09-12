using CloudCrate.Application.Constants;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class UserAccount
{
    public string Id { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string ProfilePictureUrl { get; private set; } = string.Empty;

    public SubscriptionPlan Plan { get; private set; } = SubscriptionPlan.Free;
    public long UsedStorageBytes { get; private set; } = 0;

    public long AllocatedStorageLimitBytes => PlanStorageLimits.GetLimit(Plan);
    public long RemainingStorageBytes => AllocatedStorageLimitBytes - UsedStorageBytes;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private UserAccount() { } // EF / object initializer

    internal UserAccount(string id, string email, string displayName, string profilePictureUrl,
                         SubscriptionPlan plan, long usedStorageBytes, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        ProfilePictureUrl = profilePictureUrl;
        Plan = plan;
        UsedStorageBytes = usedStorageBytes;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static UserAccount Rehydrate(string id, string email, string displayName, string profilePictureUrl,
                                        SubscriptionPlan plan, long usedStorageBytes, DateTime createdAt, DateTime updatedAt)
    {
        return new UserAccount(id, email, displayName, profilePictureUrl, plan, usedStorageBytes, createdAt, updatedAt);
    }

    public static UserAccount Create(string id, string email, string displayName = "", string profilePictureUrl = "", SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        return new UserAccount
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            ProfilePictureUrl = profilePictureUrl,
            Plan = plan,
            UsedStorageBytes = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void ChangePlan(SubscriptionPlan newPlan)
    {
        if (UsedStorageBytes > PlanStorageLimits.GetLimit(newPlan))
            throw new InvalidOperationException("Cannot downgrade plan below current used storage.");

        Plan = newPlan;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConsumeStorage(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (UsedStorageBytes + bytes > AllocatedStorageLimitBytes)
            throw new InvalidOperationException("Insufficient storage available.");

        UsedStorageBytes += bytes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseStorage(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));

        UsedStorageBytes = Math.Max(0, UsedStorageBytes - bytes);
        UpdatedAt = DateTime.UtcNow;
    }
}
