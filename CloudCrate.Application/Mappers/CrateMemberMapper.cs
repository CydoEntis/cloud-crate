using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Mappers;

public static class CrateMemberMapperExtensions
{
    public static CrateMemberResponse ToCrateMemberResponse(
        this CrateMember member,
        Dictionary<string, UserResponse> users)
    {
        users.TryGetValue(member.UserId, out var user);

        return new CrateMemberResponse
        {
            UserId = member.UserId,
            DisplayName = user?.DisplayName ?? "Unknown",
            Email = user?.Email ?? string.Empty,
            Role = member.Role,
            ProfilePicture = user?.ProfilePictureUrl ?? string.Empty,
            JoinedAt = member.JoinedAt
        };
    }
}