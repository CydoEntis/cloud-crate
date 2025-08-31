using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class CrateUser
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ProfilePictureUrl { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static CrateUser Create(
        string id,
        string displayName,
        string email,
        string profilePictureUrl,
        SubscriptionPlan plan,
        DateTime createdAt,
        DateTime updatedAt
    )
    {
        return new CrateUser
        {
            Id = id,
            DisplayName = displayName,
            Email = email,
            ProfilePictureUrl = profilePictureUrl,
            Plan = plan,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}