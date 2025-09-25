using CloudCrate.Domain.Constants;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class UserAccount
{
    public string Id { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string ProfilePictureUrl { get; private set; } = string.Empty;
    public bool IsAdmin { get; private set; } = false;

    public SubscriptionPlan Plan { get; private set; } = SubscriptionPlan.Free;

    public long AllocatedStorageBytes { get; private set; } = 0;

    public long UsedStorageBytes { get; private set; } = 0;

    public long AccountStorageLimitBytes => PlanStorageLimits.GetLimit(Plan);

    public long RemainingAllocationBytes => AccountStorageLimitBytes - AllocatedStorageBytes;

    public long RemainingUsageBytes => AccountStorageLimitBytes - UsedStorageBytes;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Soft Delete Properties
    public bool IsDeleted { get; private set; } = false;
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedByUserId { get; private set; }

    private UserAccount()
    {
    }

    internal UserAccount(string id, string email, string displayName, string profilePictureUrl,
        SubscriptionPlan plan, long allocatedStorageBytes, long usedStorageBytes,
        DateTime createdAt, DateTime updatedAt, bool isAdmin = false,
        bool isDeleted = false, DateTime? deletedAt = null, string? deletedByUserId = null)
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
        IsAdmin = isAdmin;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
        DeletedByUserId = deletedByUserId;
    }

    public static UserAccount Rehydrate(string id, string email, string displayName, string profilePictureUrl,
        SubscriptionPlan plan, long allocatedStorageBytes, long usedStorageBytes,
        DateTime createdAt, DateTime updatedAt, bool isAdmin = false,
        bool isDeleted = false, DateTime? deletedAt = null, string? deletedByUserId = null)
    {
        return new UserAccount(id, email, displayName, profilePictureUrl, plan,
            allocatedStorageBytes, usedStorageBytes, createdAt, updatedAt, isAdmin,
            isDeleted, deletedAt, deletedByUserId);
    }

    public static UserAccount Create(string id, string email, string displayName = "",
        string profilePictureUrl = "", SubscriptionPlan plan = SubscriptionPlan.Free,
        bool isAdmin = false)
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
            UpdatedAt = DateTime.UtcNow,
            IsAdmin = isAdmin,
            IsDeleted = false,
            DeletedAt = null,
            DeletedByUserId = null
        };
    }

    // Soft Delete Methods
    public void MarkAsDeleted(string deletedByUserId)
    {
        if (IsDeleted)
            throw new InvalidOperationException("User is already deleted.");

        if (string.IsNullOrEmpty(deletedByUserId))
            throw new ArgumentException("DeletedByUserId cannot be null or empty.", nameof(deletedByUserId));

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedByUserId = deletedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        if (!IsDeleted)
            throw new InvalidOperationException("User is not deleted.");

        IsDeleted = false;
        DeletedAt = null;
        DeletedByUserId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanBeDeleted()
    {
        return !IsDeleted;
    }

    public void ChangePlan(SubscriptionPlan newPlan)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot change plan for deleted user.");

        if (!Enum.IsDefined(typeof(SubscriptionPlan), newPlan))
            throw new ArgumentException($"Invalid subscription plan: {newPlan}");

        var newLimit = PlanStorageLimits.GetLimit(newPlan);

        if (AllocatedStorageBytes > newLimit)
            throw new InvalidOperationException("Cannot downgrade plan below current allocated storage.");

        if (UsedStorageBytes > newLimit)
            throw new InvalidOperationException("Cannot downgrade plan below current used storage.");

        Plan = newPlan;
        UpdatedAt = DateTime.UtcNow;
    }

    public void PromoteToAdmin()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot promote deleted user to admin.");

        IsAdmin = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RevokeAdmin()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot revoke admin from deleted user.");

        IsAdmin = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanPerformAdminActions()
    {
        return IsAdmin && !IsDeleted;
    }

    public bool CanModifyUser(UserAccount targetUser)
    {
        if (!IsAdmin || IsDeleted) return false;
        if (Id == targetUser.Id) return false;
        return true;
    }

    public void AllocateStorage(long bytes)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot allocate storage for deleted user.");

        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (AllocatedStorageBytes + bytes > AccountStorageLimitBytes)
            throw new InvalidOperationException("Insufficient storage quota available for allocation.");

        AllocatedStorageBytes += bytes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeallocateStorage(long bytes)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot deallocate storage for deleted user.");

        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));

        AllocatedStorageBytes = Math.Max(0, AllocatedStorageBytes - bytes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConsumeStorage(long bytes)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot consume storage for deleted user.");

        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (UsedStorageBytes + bytes > AccountStorageLimitBytes)
            throw new InvalidOperationException("Account storage limit exceeded.");

        UsedStorageBytes += bytes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseStorage(long bytes)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot release storage for deleted user.");

        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));

        UsedStorageBytes = Math.Max(0, UsedStorageBytes - bytes);
        UpdatedAt = DateTime.UtcNow;
    }
}