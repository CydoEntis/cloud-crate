namespace CloudCrate.Application.DTOs.Admin.Response;

public class AdminUserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProfilePictureUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Plan { get; set; } = string.Empty;
    public long UsedStorageBytes { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockoutEnd { get; set; }
}