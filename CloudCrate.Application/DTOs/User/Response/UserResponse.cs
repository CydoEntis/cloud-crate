namespace CloudCrate.Application.DTOs.User.Response;

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }

    public long AccountStorageLimitBytes { get; set; }
    public long AllocatedStorageBytes { get; set; }
    public long UsedStorageBytes { get; set; }

    public long RemainingAllocationBytes { get; set; }
    public long RemainingUsageBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsAdmin { get; set; }
}