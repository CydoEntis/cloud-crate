using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.DTOs.User.Mappers;

public static class UserMapper
{
    public static Uploader ToUploader(UserResponse user) =>
        new Uploader
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            ProfilePictureUrl = user.ProfilePictureUrl
        };
    
    public static Uploader ToUploader(CrateUser user) =>
        new Uploader
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            ProfilePictureUrl = user.ProfilePictureUrl
        };

    public static UserResponse ToUserResponse(CrateUser user) => new UserResponse
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        ProfilePictureUrl = user.ProfilePictureUrl
    };
}