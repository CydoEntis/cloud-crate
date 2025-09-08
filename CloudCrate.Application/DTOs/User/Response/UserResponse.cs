namespace CloudCrate.Application.DTOs.User.Response;

public class UserResponse
{
    public string Id { get; set; } = string.Empty!;
    public string Email { get; set; } = string.Empty!;
    public string DisplayName { get; set; } = string.Empty!;
    public string? ProfilePictureUrl { get; set; }

    public long AllocatedStorageLimitBytes { get; set; }
    public long UsedAccountStorageBytes { get; set; }

    public long RemainingAllocatableBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}