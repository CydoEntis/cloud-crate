namespace CloudCrate.Application.DTOs.User;

public class Uploader
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ProfilePictureUrl { get; set; } =  string.Empty;
}