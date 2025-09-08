using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Infrastructure.Identity;

namespace CloudCrate.Infrastructure.Services.User.Mappers;

public static class UserMapper
{
    public static UserResponse ToUserResponse(this ApplicationUser user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            AllocatedStorageLimitBytes = user.AllocatedStorageLimitBytes,   
            UsedAccountStorageBytes = user.UsedAccountStorageBytes,  
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
    
    public static UserResponse ToUserResponse(this ApplicationUser user, long allocatedSoFar)
    {
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            AllocatedStorageLimitBytes = user.AllocatedStorageLimitBytes,
            UsedAccountStorageBytes = user.UsedAccountStorageBytes,
            RemainingAllocatableBytes = user.AllocatedStorageLimitBytes - allocatedSoFar, // ✅ new field
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}