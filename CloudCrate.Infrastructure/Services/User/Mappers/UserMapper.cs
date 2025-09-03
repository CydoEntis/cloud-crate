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
            MaxStorageBytes = user.MaxStorageBytes,   
            UsedStorageBytes = user.UsedStorageBytes,  
            CreatedAt = user.CreatedAt,
            UpdadatedAt = user.UpdatedAt
        };
    }
}