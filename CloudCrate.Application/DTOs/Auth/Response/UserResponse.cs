using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Auth.Response;

public class UserResponse
{
    public string Id { get; set; }
    public string Email { get; set; }
    public int CrateLimit { get; set; }
    public long UsedStorage { get; set; }
    public int CrateCount { get; set; }

    public SubscriptionPlan Plan { get; set; }
}