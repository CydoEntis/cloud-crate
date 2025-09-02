namespace CloudCrate.Application.DTOs.User.Response;

public class UserResponse
{
    public string Id { get; set; } = string.Empty!;
    public string Email { get; set; } = string.Empty!;
    public string DisplayName { get; set; } = string.Empty!;
    public string? ProfilePictureUrl { get; set; }

    public long MaxStorageBytes { get; set; }
    public long UsedStorageBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdadatedAt { get; set; } = DateTime.UtcNow;
}