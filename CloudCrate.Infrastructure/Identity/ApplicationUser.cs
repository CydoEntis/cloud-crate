using CloudCrate.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public SubscriptionPlan Plan { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ProfilePictureUrl { get; set; } = string.Empty;
}