using CloudCrate.Domain.Constants;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class UserAccount
{
    public string Id { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string ProfilePictureUrl { get; private set; } = string.Empty;

    public SubscriptionPlan Plan { get; private set; } = SubscriptionPlan.Free;
    
    public long AllocatedStorageBytes { get; private set; } = 0;
    
    public long UsedStorageBytes { get; private set; } = 0;

    public long AccountStorageLimitBytes => PlanStorageLimits.GetLimit(Plan);
    
    public long RemainingAllocationBytes => AccountStorageLimitBytes - AllocatedStorageBytes;
    
    public long RemainingUsageBytes => AccountStorageLimitBytes - UsedStorageBytes;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private UserAccount() { } 

    internal UserAccount(string id, string email, string displayName, string profilePictureUrl,
                         SubscriptionPlan plan, long allocatedStorageBytes, long usedStorageBytes, 
                         DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        ProfilePictureUrl = profilePictureUrl;
        Plan = plan;
        AllocatedStorageBytes = allocatedStorageBytes;
        UsedStorageBytes = usedStorageBytes;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static UserAccount Rehydrate(string id, string email, string displayName, string profilePictureUrl,
                                        SubscriptionPlan plan, long allocatedStorageBytes, long usedStorageBytes, 
                                        DateTime createdAt, DateTime updatedAt)
    {
        return new UserAccount(id, email, displayName, profilePictureUrl, plan, 
                              allocatedStorageBytes, usedStorageBytes, createdAt, updatedAt);
    }

    public static UserAccount Create(string id, string email, string displayName = "", 
                                   string profilePictureUrl = "", SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        return new UserAccount
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            ProfilePictureUrl = profilePictureUrl,
            Plan = plan,
            AllocatedStorageBytes = 0,
            UsedStorageBytes = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void ChangePlan(SubscriptionPlan newPlan)
    {
        var newLimit = PlanStorageLimits.GetLimit(newPlan);
        
        if (AllocatedStorageBytes > newLimit)
            throw new InvalidOperationException("Cannot downgrade plan below current allocated storage.");
            
        if (UsedStorageBytes > newLimit)
            throw new InvalidOperationException("Cannot downgrade plan below current used storage.");

        Plan = newPlan;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AllocateStorage(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (AllocatedStorageBytes + bytes > AccountStorageLimitBytes)
            throw new InvalidOperationException("Insufficient storage quota available for allocation.");

        AllocatedStorageBytes += bytes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeallocateStorage(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));

        AllocatedStorageBytes = Math.Max(0, AllocatedStorageBytes - bytes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConsumeStorage(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (UsedStorageBytes + bytes > AccountStorageLimitBytes)
            throw new InvalidOperationException("Account storage limit exceeded.");

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