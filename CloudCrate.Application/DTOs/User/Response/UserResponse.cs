namespace CloudCrate.Application.DTOs.User.Response;

public class UserResponse
{
    public string Id { get; set; } = string.Empty!;
    public string Email { get; set; } = string.Empty!;
    public string DisplayName { get; set; } = string.Empty!;
    public string? ProfilePictureUrl { get; set; }
}