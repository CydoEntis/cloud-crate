using CloudCrate.Application.DTOs.User.Projections;
using CloudCrate.Application.DTOs.User.Response;

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
}