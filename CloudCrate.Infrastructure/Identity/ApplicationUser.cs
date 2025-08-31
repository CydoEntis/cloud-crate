using System.ComponentModel.DataAnnotations;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public SubscriptionPlan Plan { get; set; }
    [MaxLength(100)] 
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ProfilePictureUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdadatedAt { get; set; } = DateTime.UtcNow;


    public CrateUser ToDomainUser() =>
        CrateUser.Create(
            Id,
            DisplayName,
            Email,
            ProfilePictureUrl,
            Plan,
            CreatedAt,
            UpdadatedAt
        );
}