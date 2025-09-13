using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Mappers;

public static class CrateMemberResponseMapper
{
    public static CrateMemberResponse ToResponse(this CrateMember member, UserAccount user)
    {
        return new CrateMemberResponse
        {
            UserId = member.UserId,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = member.Role,
            ProfilePicture = user.ProfilePictureUrl,
            JoinedAt = member.JoinedAt
        };
    }
}