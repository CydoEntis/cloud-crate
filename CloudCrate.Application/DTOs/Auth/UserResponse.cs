namespace CloudCrate.Application.DTOs.Auth;

public class UserResponse
{
    public string Id { get; set; }
    public string Email { get; set; }
    public int CrateLimit { get; set; }
    public long UsedStorage { get; set; }
    public int CrateCount { get; set; }
}