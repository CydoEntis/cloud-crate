using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Mappers;

public static class CrateMapper
{
    public static CrateListItemResponse ToCrateListItemResponse(
        this Domain.Entities.Crate crate,
        string userId,
        Dictionary<string, UserResponse> users) 
    {
        var owner = crate.Members.FirstOrDefault(m => m.Role == CrateRole.Owner);
        var joined = crate.Members.FirstOrDefault(m => m.UserId == userId);

        return new CrateListItemResponse
        {
            Id = crate.Id,
            Name = crate.Name,
            Color = crate.Color,
            Owner = owner is null
                ? null!
                : new CrateMemberResponse
                {
                    UserId = owner.UserId,
                    DisplayName = users[owner.UserId].DisplayName,
                    ProfilePicture = users[owner.UserId].ProfilePictureUrl,
                    JoinedAt = owner.JoinedAt,
                    Role = CrateRole.Owner
                },
            UsedStorageBytes = crate.UsedStorage.Bytes,
            TotalStorageBytes = crate.AllocatedStorage.Bytes,
            CratedAt = joined?.JoinedAt ?? DateTime.MinValue
        };
    }
}