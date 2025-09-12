using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CloudCrate.Domain.Constants;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)] 
    public string DisplayName { get; set; } = string.Empty;
    
    [MaxLength(500)] 
    public string ProfilePictureUrl { get; set; } = string.Empty;
    
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    
    public long AllocatedStorageBytes { get; set; } = 0;
    
    public long UsedStorageBytes { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual ICollection<CrateEntity> Crates { get; set; } = new List<CrateEntity>();
    
    [NotMapped]
    public long AccountStorageLimitBytes => PlanStorageLimits.GetLimit(Plan);
    
    [NotMapped]
    public long RemainingAllocationBytes => AccountStorageLimitBytes - AllocatedStorageBytes;
    
    [NotMapped]
    public long RemainingUsageBytes => AccountStorageLimitBytes - UsedStorageBytes;

    public long GetTotalStorageAllowanceBytes() => PlanStorageLimits.GetLimit(Plan);
}