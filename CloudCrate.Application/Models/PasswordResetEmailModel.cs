namespace CloudCrate.Application.Models;

public class PasswordResetEmailModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string ResetLink { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}