using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.User.Response;

public class UserProfileResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; }
    public double TotalStorageMb { get; set; }
    public double UsedStorageMb { get; set; }
    public bool CanCreateMoreCrates { get; set; }
    public int CrateLimit { get; set; }
}