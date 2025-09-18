using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Mappers;

public static class CrateMemberResponseMapper
{
    public static CrateMemberResponse ToResponse(this CrateMember member)
    {
        return new CrateMemberResponse
        {
            UserId = member.UserId,
            DisplayName = member.User.DisplayName,
            Email = member.User.Email,
            Role = member.Role,
            ProfilePicture = member.User.ProfilePictureUrl,
            JoinedAt = member.JoinedAt
        };
    }
}