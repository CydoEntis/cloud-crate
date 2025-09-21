using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Mappers;

public static class CrateSummaryMapper
{
    public static CrateSummaryResponse ToCrateSummaryResponse(
        this Domain.Entities.Crate crate,
        string userId)
    {
        var owner = crate.Members.FirstOrDefault(m => m.Role == CrateRole.Owner);
        var joined = crate.Members.FirstOrDefault(m => m.UserId == userId);

        return new CrateSummaryResponse
        {
            Id = crate.Id,
            Name = crate.Name,
            Color = crate.Color,
            Owner = owner?.User is null
                ? null!
                : new CrateMemberResponse
                {
                    UserId = owner.UserId,
                    DisplayName = owner.User.DisplayName,
                    Email = owner.User.Email,
                    ProfilePicture = owner.User.ProfilePictureUrl,
                    JoinedAt = owner.JoinedAt,
                    Role = CrateRole.Owner
                },
            UsedStorageBytes = crate.UsedStorage.Bytes,
            AllocatedStorageBytes = crate.AllocatedStorage.Bytes,
            JoinedAt = joined?.JoinedAt ?? DateTime.MinValue,
            CurrentUserRole = joined?.Role ?? CrateRole.Member
        };
    }
}